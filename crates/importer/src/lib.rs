use domain::{Jan, JanError};

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

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ImportRow {
    pub parent_sku: String,
    pub sku: String,
    pub jan: Jan,
    pub qty: u32,
    pub brand: String,
    pub template: String,
    pub printer_profile: String,
    pub enabled: bool,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct CellError {
    pub row_number: usize,
    pub column: String,
    pub message: String,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum RowError {
    InvalidColumnCount {
        row_number: usize,
        expected: usize,
        actual: usize,
    },
    InvalidCells(Vec<CellError>),
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

pub fn validate_row(row_number: usize, values: &[&str]) -> Result<ImportRow, RowError> {
    if values.len() != REQUIRED_COLUMNS.len() {
        return Err(RowError::InvalidColumnCount {
            row_number,
            expected: REQUIRED_COLUMNS.len(),
            actual: values.len(),
        });
    }

    let parent_sku = values[0].trim().to_string();
    let sku = values[1].trim().to_string();
    let jan_raw = values[2].trim();
    let qty_raw = values[3].trim();
    let brand = values[4].trim().to_string();
    let template = values[5].trim().to_string();
    let printer_profile = values[6].trim().to_string();
    let enabled_raw = values[7].trim();

    let mut errors = Vec::new();

    if parent_sku.is_empty() {
        errors.push(required_field_error(row_number, "parent_sku"));
    }

    if sku.is_empty() {
        errors.push(required_field_error(row_number, "sku"));
    }

    let jan = match Jan::parse(jan_raw) {
        Ok(jan) => Some(jan),
        Err(error) => {
            errors.push(CellError {
                row_number,
                column: "jan".to_string(),
                message: jan_error_message(error),
            });
            None
        }
    };

    let qty = match qty_raw.parse::<u32>() {
        Ok(qty) if qty > 0 => Some(qty),
        Ok(_) => {
            errors.push(CellError {
                row_number,
                column: "qty".to_string(),
                message: "must be an integer greater than or equal to 1".to_string(),
            });
            None
        }
        Err(_) => {
            errors.push(CellError {
                row_number,
                column: "qty".to_string(),
                message: "must be an integer greater than or equal to 1".to_string(),
            });
            None
        }
    };

    if brand.is_empty() {
        errors.push(required_field_error(row_number, "brand"));
    }

    if template.is_empty() {
        errors.push(required_field_error(row_number, "template"));
    }

    if printer_profile.is_empty() {
        errors.push(required_field_error(row_number, "printer_profile"));
    }

    let enabled = match enabled_raw {
        "true" => Some(true),
        "false" => Some(false),
        _ => {
            errors.push(CellError {
                row_number,
                column: "enabled".to_string(),
                message: "must be 'true' or 'false'".to_string(),
            });
            None
        }
    };

    if !errors.is_empty() {
        return Err(RowError::InvalidCells(errors));
    }

    Ok(ImportRow {
        parent_sku,
        sku,
        jan: jan.expect("validated jan must exist"),
        qty: qty.expect("validated qty must exist"),
        brand,
        template,
        printer_profile,
        enabled: enabled.expect("validated enabled must exist"),
    })
}

fn required_field_error(row_number: usize, column: &str) -> CellError {
    CellError {
        row_number,
        column: column.to_string(),
        message: "must not be empty".to_string(),
    }
}

fn jan_error_message(error: JanError) -> String {
    match error {
        JanError::Empty => "must not be empty".to_string(),
        JanError::NonDigit => "must contain digits only".to_string(),
        JanError::InvalidLength { actual } => {
            format!("must be 12 or 13 digits, got {actual}")
        }
        JanError::InvalidChecksum { expected, actual } => {
            format!("invalid checksum: expected {expected}, got {actual}")
        }
    }
}

#[cfg(test)]
mod tests {
    use super::{validate_headers, validate_row, CellError, HeaderError, RowError};

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

    #[test]
    fn validates_row_and_normalizes_jan() {
        let (_, row) = fixture_row(include_str!(
            "../../../packages/fixtures/importer/catalog-valid.csv"
        ));

        let actual = validate_row(2, &row).expect("valid fixture row must pass");

        assert_eq!(actual.parent_sku, "PARENT-0001");
        assert_eq!(actual.sku, "SKU-0001");
        assert_eq!(actual.jan.as_str(), "4006381333931");
        assert_eq!(actual.qty, 24);
        assert_eq!(actual.brand, "Acme");
        assert_eq!(actual.template, "basic-50x30");
        assert_eq!(actual.printer_profile, "pdf-a4-proof");
        assert!(actual.enabled);
    }

    #[test]
    fn returns_cell_level_errors_for_invalid_values() {
        let (_, row) = fixture_row(include_str!(
            "../../../packages/fixtures/importer/catalog-invalid-values.csv"
        ));

        let err = validate_row(2, &row).expect_err("invalid fixture row must fail");

        assert_eq!(
            err,
            RowError::InvalidCells(vec![
                CellError {
                    row_number: 2,
                    column: "sku".to_string(),
                    message: "must not be empty".to_string(),
                },
                CellError {
                    row_number: 2,
                    column: "jan".to_string(),
                    message: "invalid checksum: expected 1, got 0".to_string(),
                },
                CellError {
                    row_number: 2,
                    column: "qty".to_string(),
                    message: "must be an integer greater than or equal to 1".to_string(),
                },
                CellError {
                    row_number: 2,
                    column: "brand".to_string(),
                    message: "must not be empty".to_string(),
                },
                CellError {
                    row_number: 2,
                    column: "printer_profile".to_string(),
                    message: "must not be empty".to_string(),
                },
                CellError {
                    row_number: 2,
                    column: "enabled".to_string(),
                    message: "must be 'true' or 'false'".to_string(),
                },
            ])
        );
    }

    #[test]
    fn rejects_rows_with_wrong_column_count() {
        let row = ["PARENT-0001", "SKU-0001", "400638133393", "24"];

        let err = validate_row(3, &row).expect_err("short rows must fail");

        assert_eq!(
            err,
            RowError::InvalidColumnCount {
                row_number: 3,
                expected: 8,
                actual: 4,
            }
        );
    }

    fn fixture_row(csv: &str) -> (Vec<String>, Vec<&str>) {
        let mut lines = csv.lines();
        let header = lines
            .next()
            .expect("fixture must include header")
            .split(',')
            .map(|value| value.to_string())
            .collect::<Vec<_>>();
        let row = lines
            .next()
            .expect("fixture must include a data row")
            .split(',')
            .collect::<Vec<_>>();
        (header, row)
    }
}
