use audit_log::{AuditActor, AuditEventKind, PrintAuditRecord, PrintJobId};
use barcode::{BarcodeEngine, BarcodeRequest};
use printer_adapters::{PrintArtifact, PrinterAdapter};
use render::{render_svg, RenderLabelRequest};

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
        let _barcode = self
            .barcode_engine
            .render(&BarcodeRequest {
                jan: request.jan.clone(),
                format: barcode::BarcodeFormat::Jan13,
                output_path: format!("tmp/{}.svg", request.job_id),
            })
            .map_err(|err| err.message)?;

        let svg = render_svg(&RenderLabelRequest {
            job_id: request.job_id.clone(),
            sku: request.sku,
            brand: request.brand,
            jan: request.jan,
            qty: request.qty,
            template_version: request.template_version,
        });

        self.adapter
            .submit(&PrintArtifact {
                media_type: "image/svg+xml".to_string(),
                bytes: svg.into_bytes(),
            })
            .map_err(|err| err.message)?;

        Ok(PrintAuditRecord {
            job_id: PrintJobId(request.job_id),
            actor: AuditActor {
                user_id: request.actor_user_id,
                display_name: request.actor_display_name,
            },
            event: AuditEventKind::Submitted,
            occurred_at: request.requested_at,
            reason: None,
        })
    }
}
