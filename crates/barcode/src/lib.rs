#[derive(Debug, Clone, PartialEq, Eq)]
pub enum BarcodeFormat {
    Jan13,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct BarcodeRequest {
    pub jan: String,
    pub format: BarcodeFormat,
    pub output_path: String,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct BarcodeArtifact {
    pub output_path: String,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct BarcodeError {
    pub message: String,
}

pub trait BarcodeEngine {
    fn render(&self, request: &BarcodeRequest) -> Result<BarcodeArtifact, BarcodeError>;
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ZintCli {
    pub binary_path: String,
}

impl ZintCli {
    pub fn command_line(&self, request: &BarcodeRequest) -> Vec<String> {
        vec![
            self.binary_path.clone(),
            "--data".to_string(),
            request.jan.clone(),
            "--output".to_string(),
            request.output_path.clone(),
        ]
    }
}

