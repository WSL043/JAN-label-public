pub const REQUIRED_COLUMNS: &[&str] = &[
    "parent_sku",
    "sku",
    "jan",
    "qty",
    "brand",
    "template",
    "printer_profile",
    "enabled",
];

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum HeaderError {
    MissingColumns(Vec<String>),
}

pub fn validate_headers(headers: &[&str]) -> Result<(), HeaderError> {
    let missing = REQUIRED_COLUMNS
        .iter()
        .filter(|column| !headers.contains(column))
        .map(|column| (*column).to_string())
        .collect::<Vec<_>>();

    if missing.is_empty() {
        Ok(())
    } else {
        Err(HeaderError::MissingColumns(missing))
    }
}

#[cfg(test)]
mod tests {
    use super::{validate_headers, HeaderError};

    #[test]
    fn accepts_expected_headers() {
        let headers = [
            "parent_sku",
            "sku",
            "jan",
            "qty",
            "brand",
            "template",
            "printer_profile",
            "enabled",
        ];

        assert_eq!(validate_headers(&headers), Ok(()));
    }

    #[test]
    fn rejects_missing_headers() {
        let headers = ["parent_sku", "sku", "jan"];
        let err = validate_headers(&headers).expect_err("missing columns must fail");
        assert_eq!(
            err,
            HeaderError::MissingColumns(vec![
                "qty".to_string(),
                "brand".to_string(),
                "template".to_string(),
                "printer_profile".to_string(),
                "enabled".to_string(),
            ])
        );
    }
}
