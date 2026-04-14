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

