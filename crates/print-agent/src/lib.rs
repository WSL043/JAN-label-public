use audit_log::{AuditActor, PrintAuditRecord, PrintJobId, PrintJobLineageId};
use barcode::{BarcodeEngine, BarcodeRequest};
use printer_adapters::{PrintArtifact, PrinterAdapter, PrinterAdapterKind};
use render::{render_pdf, render_svg, RenderLabelRequest};

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct DispatchRequest {
    pub job_id: String,
    pub sku: String,
    pub brand: String,
    pub jan: String,
    pub qty: u32,
    pub template_version: String,
    pub actor_user_id: String,
    pub actor_display_name: String,
    pub requested_at: String,
    pub job_lineage_id: Option<String>,
    pub reprint_of_job_id: Option<String>,
    pub reason: Option<String>,
}

pub struct PrintAgent<A, B>
where
    A: PrinterAdapter,
    B: BarcodeEngine,
{
    adapter: A,
    barcode_engine: B,
}

impl<A, B> PrintAgent<A, B>
where
    A: PrinterAdapter,
    B: BarcodeEngine,
{
    pub fn new(adapter: A, barcode_engine: B) -> Self {
        Self {
            adapter,
            barcode_engine,
        }
    }

    pub fn dispatch(&self, request: DispatchRequest) -> Result<PrintAuditRecord, String> {
        let DispatchRequest {
            job_id,
            sku,
            brand,
            jan,
            qty,
            template_version,
            actor_user_id,
            actor_display_name,
            requested_at,
            job_lineage_id,
            reprint_of_job_id,
            reason,
        } = request;

        let lineage_id = job_lineage_id
            .clone()
            .or_else(|| reprint_of_job_id.clone())
            .unwrap_or_else(|| job_id.clone());

        let _barcode = self
            .barcode_engine
            .render(&BarcodeRequest {
                jan: jan.clone(),
                format: barcode::BarcodeFormat::Jan13,
                output_path: format!("tmp/{}.svg", job_id),
            })
            .map_err(|err| err.message)?;

        let render_request = RenderLabelRequest {
            job_id: job_id.clone(),
            sku,
            brand,
            jan,
            qty,
            template_version,
        };

        let artifact = match self.adapter.kind() {
            PrinterAdapterKind::Pdf => PrintArtifact {
                media_type: "application/pdf".to_string(),
                bytes: render_pdf(&render_request),
            },
            _ => PrintArtifact {
                media_type: "image/svg+xml".to_string(),
                bytes: render_svg(&render_request).into_bytes(),
            },
        };

        self.adapter.submit(&artifact).map_err(|err| err.message)?;

        Ok(PrintAuditRecord::dispatch(
            PrintJobId(job_id),
            PrintJobLineageId(lineage_id),
            reprint_of_job_id.map(PrintJobId),
            AuditActor {
                user_id: actor_user_id,
                display_name: actor_display_name,
            },
            requested_at,
            reason,
        ))
    }
}

#[cfg(test)]
mod tests {
    use super::{DispatchRequest, PrintAgent};
    use audit_log::{AuditEventKind, PrintJobId, PrintJobLineageId};
    use barcode::{BarcodeArtifact, BarcodeEngine, BarcodeError, BarcodeFormat, BarcodeRequest};
    use printer_adapters::{
        AdapterError, PrintArtifact, PrinterAdapter, PrinterAdapterKind, SubmissionReceipt,
    };
    use std::sync::{Arc, Mutex};

    #[test]
    fn dispatch_records_submitted_event_for_original_job() {
        let adapter = FakeAdapter::new(PrinterAdapterKind::Pdf);
        let barcode_engine = FakeBarcodeEngine;
        let agent = PrintAgent::new(adapter.clone(), barcode_engine);

        let record = agent
            .dispatch(sample_request())
            .expect("original dispatch should succeed");
        let submission = adapter.last_submission();

        assert_eq!(record.event, AuditEventKind::Submitted);
        assert_eq!(
            record.job_lineage_id,
            PrintJobLineageId("JOB-20260415-0001".to_string())
        );
        assert_eq!(record.parent_job_id, None);
        assert_eq!(record.reason, None);
        assert_eq!(submission.media_type, "application/pdf");
        assert!(
            submission.bytes.starts_with(b"%PDF-1.4"),
            "pdf proof output should be rendered as a PDF artifact"
        );
    }

    #[test]
    fn dispatch_records_reprint_event_with_lineage_and_reason() {
        let adapter = FakeAdapter::new(PrinterAdapterKind::Pdf);
        let barcode_engine = FakeBarcodeEngine;
        let agent = PrintAgent::new(adapter, barcode_engine);
        let request = DispatchRequest {
            job_id: "JOB-20260415-0002".to_string(),
            job_lineage_id: Some("JOB-20260415-0001".to_string()),
            reprint_of_job_id: Some("JOB-20260415-0001".to_string()),
            reason: Some("damaged label".to_string()),
            ..sample_request()
        };

        let record = agent
            .dispatch(request)
            .expect("reprint dispatch should succeed");

        assert_eq!(record.event, AuditEventKind::Reprinted);
        assert_eq!(record.job_id, PrintJobId("JOB-20260415-0002".to_string()));
        assert_eq!(
            record.job_lineage_id,
            PrintJobLineageId("JOB-20260415-0001".to_string())
        );
        assert_eq!(
            record.parent_job_id,
            Some(PrintJobId("JOB-20260415-0001".to_string()))
        );
        assert_eq!(record.reason, Some("damaged label".to_string()));
    }

    #[test]
    fn dispatch_uses_parent_job_as_lineage_when_explicit_lineage_is_missing() {
        let adapter = FakeAdapter::new(PrinterAdapterKind::Pdf);
        let barcode_engine = FakeBarcodeEngine;
        let agent = PrintAgent::new(adapter, barcode_engine);
        let request = DispatchRequest {
            job_id: "JOB-20260415-0003".to_string(),
            job_lineage_id: None,
            reprint_of_job_id: Some("JOB-20260415-0001".to_string()),
            reason: Some("operator retry".to_string()),
            ..sample_request()
        };

        let record = agent
            .dispatch(request)
            .expect("reprint dispatch should succeed");

        assert_eq!(
            record.job_lineage_id,
            PrintJobLineageId("JOB-20260415-0001".to_string())
        );
        assert_eq!(record.event, AuditEventKind::Reprinted);
    }

    #[test]
    fn dispatch_uses_svg_for_windows_spooler_adapter() {
        let adapter = FakeAdapter::new(PrinterAdapterKind::WindowsSpooler);
        let barcode_engine = FakeBarcodeEngine;
        let agent = PrintAgent::new(adapter.clone(), barcode_engine);

        agent
            .dispatch(sample_request())
            .expect("non-pdf dispatch should succeed");

        let submission = adapter.last_submission();
        assert_eq!(submission.media_type, "image/svg+xml");
        assert!(
            submission.bytes.starts_with(b"<svg"),
            "windows spooler adapters should keep receiving SVG artifacts"
        );
    }

    fn sample_request() -> DispatchRequest {
        DispatchRequest {
            job_id: "JOB-20260415-0001".to_string(),
            sku: "SKU-0001".to_string(),
            brand: "Acme".to_string(),
            jan: "4006381333931".to_string(),
            qty: 24,
            template_version: "basic-50x30@v1".to_string(),
            actor_user_id: "ops.user".to_string(),
            actor_display_name: "Ops User".to_string(),
            requested_at: "2026-04-15T09:00:00Z".to_string(),
            job_lineage_id: None,
            reprint_of_job_id: None,
            reason: None,
        }
    }

    #[derive(Clone)]
    struct FakeAdapter {
        kind: PrinterAdapterKind,
        submissions: Arc<Mutex<Vec<PrintArtifact>>>,
    }

    impl FakeAdapter {
        fn new(kind: PrinterAdapterKind) -> Self {
            Self {
                kind,
                submissions: Arc::new(Mutex::new(Vec::new())),
            }
        }

        fn last_submission(&self) -> PrintArtifact {
            self.submissions
                .lock()
                .expect("fake adapter submissions lock should be available")
                .last()
                .cloned()
                .expect("a submission should have been recorded")
        }
    }

    impl PrinterAdapter for FakeAdapter {
        fn kind(&self) -> PrinterAdapterKind {
            self.kind.clone()
        }

        fn submit(&self, artifact: &PrintArtifact) -> Result<SubmissionReceipt, AdapterError> {
            self.submissions
                .lock()
                .expect("fake adapter submissions lock should be available")
                .push(artifact.clone());

            Ok(SubmissionReceipt {
                adapter_kind: PrinterAdapterKind::Pdf,
                external_job_id: "proof-0001".to_string(),
            })
        }
    }

    struct FakeBarcodeEngine;

    impl BarcodeEngine for FakeBarcodeEngine {
        fn render(&self, request: &BarcodeRequest) -> Result<BarcodeArtifact, BarcodeError> {
            assert_eq!(request.format, BarcodeFormat::Jan13);

            Ok(BarcodeArtifact {
                output_path: request.output_path.clone(),
            })
        }
    }
}
