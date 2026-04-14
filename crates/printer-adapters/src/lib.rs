use std::fs;
use std::path::Path;

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum PrinterAdapterKind {
    Pdf,
    WindowsSpooler,
    Zpl,
    Tspl,
    Qz,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct PrintArtifact {
    pub media_type: String,
    pub bytes: Vec<u8>,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct SubmissionReceipt {
    pub adapter_kind: PrinterAdapterKind,
    pub external_job_id: String,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct AdapterError {
    pub message: String,
}

pub trait PrinterAdapter {
    fn kind(&self) -> PrinterAdapterKind;
    fn submit(&self, artifact: &PrintArtifact) -> Result<SubmissionReceipt, AdapterError>;
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct PdfFileAdapter {
    pub output_path: String,
}

impl PrinterAdapter for PdfFileAdapter {
    fn kind(&self) -> PrinterAdapterKind {
        PrinterAdapterKind::Pdf
    }

    fn submit(&self, artifact: &PrintArtifact) -> Result<SubmissionReceipt, AdapterError> {
        if artifact.media_type != "application/pdf" {
            return Err(AdapterError {
                message: format!(
                    "pdf adapter requires application/pdf artifact, got '{}'",
                    artifact.media_type
                ),
            });
        }

        if let Some(parent) = Path::new(&self.output_path).parent() {
            if !parent.as_os_str().is_empty() {
                fs::create_dir_all(parent).map_err(|error| AdapterError {
                    message: format!(
                        "failed to create pdf proof directory '{}': {error}",
                        parent.display()
                    ),
                })?;
            }
        }

        fs::write(&self.output_path, &artifact.bytes).map_err(|error| AdapterError {
            message: format!("failed to write pdf proof '{}': {error}", self.output_path),
        })?;

        Ok(SubmissionReceipt {
            adapter_kind: PrinterAdapterKind::Pdf,
            external_job_id: self.output_path.clone(),
        })
    }
}

#[cfg(test)]
mod tests {
    use super::{PdfFileAdapter, PrintArtifact, PrinterAdapter, PrinterAdapterKind};
    use std::env;
    use std::fs;
    use std::path::{Path, PathBuf};
    use std::process;
    use std::time::{SystemTime, UNIX_EPOCH};

    #[test]
    fn pdf_file_adapter_writes_pdf_artifact_to_disk() {
        let temp_dir = TestDir::new();
        let output_path = temp_dir.path().join("proofs").join("label-proof.pdf");
        let adapter = PdfFileAdapter {
            output_path: output_path.to_string_lossy().into_owned(),
        };

        let receipt = adapter
            .submit(&PrintArtifact {
                media_type: "application/pdf".to_string(),
                bytes: b"%PDF-1.4\nproof\n".to_vec(),
            })
            .expect("pdf adapter should write proof output");

        assert_eq!(receipt.adapter_kind, PrinterAdapterKind::Pdf);
        assert_eq!(receipt.external_job_id, adapter.output_path);
        assert_eq!(
            fs::read(&output_path).expect("written pdf proof should exist"),
            b"%PDF-1.4\nproof\n".to_vec()
        );
    }

    #[test]
    fn pdf_file_adapter_rejects_non_pdf_artifacts() {
        let temp_dir = TestDir::new();
        let adapter = PdfFileAdapter {
            output_path: temp_dir
                .path()
                .join("proof.svg")
                .to_string_lossy()
                .into_owned(),
        };

        let err = adapter
            .submit(&PrintArtifact {
                media_type: "image/svg+xml".to_string(),
                bytes: b"<svg />".to_vec(),
            })
            .expect_err("pdf adapter should reject non-pdf artifacts");

        assert_eq!(
            err.message,
            "pdf adapter requires application/pdf artifact, got 'image/svg+xml'"
        );
    }

    struct TestDir {
        path: PathBuf,
    }

    impl TestDir {
        fn new() -> Self {
            let unique = SystemTime::now()
                .duration_since(UNIX_EPOCH)
                .expect("time should move forward")
                .as_nanos();
            let path = env::temp_dir().join(format!("pdf-adapter-test-{}-{unique}", process::id()));
            fs::create_dir_all(&path).expect("temp test dir must be created");
            Self { path }
        }

        fn path(&self) -> &Path {
            &self.path
        }
    }

    impl Drop for TestDir {
        fn drop(&mut self) {
            let _ = fs::remove_dir_all(&self.path);
        }
    }
}
