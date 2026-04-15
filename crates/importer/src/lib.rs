use domain::{Jan, JanError};

pub const REQUIRED_COLUMNS: [&str; 8] = [
    "parent_sku",
    "sku",
    "jan",
    "qty",
    "brand",
    "template",
    "printer_profile",
    "enabled",
];

pub type HeaderIndexMap = [usize; 8];

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum HeaderError {
    MissingColumns(Vec<String>),
    DuplicateColumns(Vec<String>),
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
    HeaderValidation(HeaderError),
    InvalidColumnCount {
        row_number: usize,
        expected: usize,
        actual: usize,
    },
    InvalidCells(Vec<CellError>),
}

#[derive(Debug, Clone, PartialEq, Eq, Copy)]
pub enum HeaderMatchQuality {
    Exact,
    Alias,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct HeaderMatch {
    pub incoming_index: usize,
    pub incoming_header: String,
    pub canonical_column: Option<String>,
    pub match_quality: Option<HeaderMatchQuality>,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct HeaderResolution {
    pub header_matches: Vec<HeaderMatch>,
    pub unused_headers: Vec<String>,
    pub unresolved_required: Vec<String>,
    pub duplicate_columns: Vec<String>,
    pub alias_mapped_required_columns: Vec<String>,
    pub safe_to_auto_apply: bool,
    pub can_validate: bool,
    pub header_indexes: Option<HeaderIndexMap>,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct BatchValidationResult {
    pub header_indexes: HeaderIndexMap,
    pub row_results: Vec<BatchRowResult>,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum BatchRowResult {
    Valid { row_number: usize, row: ImportRow },
    Invalid { row_number: usize, error: RowError },
}

const HEADER_ALIASES: &[(&str, &[&str])] = &[
    (
        "parent_sku",
        &[
            "parent_sku",
            "parent sku",
            "parent-sku",
            "parentsku",
            "parentitem",
            "parent item",
            "parentitemcode",
            "parent item code",
            "parent_sku_code",
            "親商品",
            "親SKU",
            "親sku",
            "親商品コード",
            "親商品 番号",
            "親アイテムコード",
        ],
    ),
    (
        "sku",
        &[
            "sku",
            "product_sku",
            "product sku",
            "product-sku",
            "item_sku",
            "item sku",
            "item-sku",
            "itemcode",
            "item code",
            "item-code",
            "productcode",
            "product code",
            "商品コード",
            "商品 番号",
            "商品番号",
            "品番",
            "品目番号",
        ],
    ),
    (
        "jan",
        &[
            "jan",
            "jancode",
            "jan code",
            "barcode",
            "barcode_no",
            "barcode no",
            "ean",
            "ean13",
            "gtin",
            "janコード",
            "バーコード",
            "バーコード番号",
            "バーコードNo",
            "jan no",
        ],
    ),
    (
        "qty",
        &[
            "qty",
            "quantity",
            "qte",
            "count",
            "print_qty",
            "print qty",
            "quantity to print",
            "入数",
            "数量",
            "印刷枚数",
            "印刷数",
            "印刷部数",
            "printcount",
            "print_count",
        ],
    ),
    (
        "brand",
        &[
            "brand",
            "brand_name",
            "brand name",
            "maker",
            "manufacturer",
            "company",
            "メーカー",
            "メーカー名",
            "ブランド",
            "ブランド名",
        ],
    ),
    (
        "template",
        &[
            "template",
            "template_name",
            "template name",
            "label_template",
            "label template",
            "design",
            "layout",
            "ラベル名",
            "テンプレート名",
            "テンプレート",
            "レイアウト名",
        ],
    ),
    (
        "printer_profile",
        &[
            "printer_profile",
            "printer profile",
            "printer",
            "printername",
            "printer name",
            "printer_profile_name",
            "printer profile name",
            "profile",
            "print_profile",
            "プリンター",
            "プリンタ",
            "プリンター名",
            "プリンタ名",
        ],
    ),
    (
        "enabled",
        &[
            "enabled",
            "is_enabled",
            "is enabled",
            "active",
            "on",
            "print_enabled",
            "print enabled",
            "有効",
            "使用可",
            "使用可能",
            "有効フラグ",
        ],
    ),
];

fn normalize_header_value(header: &str) -> String {
    header
        .trim()
        .to_lowercase()
        .chars()
        .filter(|ch| ch.is_alphanumeric())
        .collect()
}

fn canonical_column_index(column: &str) -> Option<usize> {
    REQUIRED_COLUMNS.iter().position(|item| item == &column)
}

fn is_exact_canonical_match(header: &str, canonical: &str) -> bool {
    header.trim().eq_ignore_ascii_case(canonical)
}

fn resolve_header_alias(header: &str) -> Option<(&'static str, HeaderMatchQuality)> {
    let normalized = normalize_header_value(header);
    if normalized.is_empty() {
        return None;
    }

    HEADER_ALIASES.iter().find_map(|(canonical, aliases)| {
        if is_exact_canonical_match(header, canonical) {
            return Some((*canonical, HeaderMatchQuality::Exact));
        }

        aliases
            .iter()
            .any(|alias| normalized == normalize_header_value(alias))
            .then_some((*canonical, HeaderMatchQuality::Alias))
    })
}

/// Return index mapping for required columns in canonical order.
pub fn resolve_header_indexes(headers: &[&str]) -> Result<HeaderIndexMap, HeaderError> {
    let resolution = inspect_header_resolution(headers);

    if !resolution.unresolved_required.is_empty() {
        return Err(HeaderError::MissingColumns(resolution.unresolved_required));
    }

    if !resolution.duplicate_columns.is_empty() {
        return Err(HeaderError::DuplicateColumns(resolution.duplicate_columns));
    }

    Ok(resolution
        .header_indexes
        .expect("header indexes exist when resolution is valid"))
}

pub fn inspect_header_resolution(headers: &[&str]) -> HeaderResolution {
    let mut canonical_indexes: [Vec<usize>; 8] = std::array::from_fn(|_| Vec::new());
    let mut header_matches = Vec::with_capacity(headers.len());

    for (idx, header) in headers.iter().enumerate() {
        let resolved = resolve_header_alias(header);

        if let Some((canonical, _quality)) = resolved {
            if let Some(canonical_index) = canonical_column_index(canonical) {
                canonical_indexes[canonical_index].push(idx);
            }
        }

        header_matches.push(HeaderMatch {
            incoming_index: idx,
            incoming_header: (*header).to_string(),
            canonical_column: (resolved.map(|(canonical, _)| canonical.to_string())),
            match_quality: resolved.map(|(_, quality)| quality),
        });
    }

    let mut duplicate_columns = Vec::new();
    let mut index_map: [Option<usize>; 8] = [None; 8];

    for i in 0..REQUIRED_COLUMNS.len() {
        match canonical_indexes[i].as_slice() {
            [idx] => index_map[i] = Some(*idx),
            [] => {}
            _ => duplicate_columns.push(REQUIRED_COLUMNS[i].to_string()),
        }
    }

    let unresolved_required = REQUIRED_COLUMNS
        .iter()
        .enumerate()
        .filter_map(|(index, column)| {
            (canonical_indexes[index].is_empty()).then_some((*column).to_string())
        })
        .collect::<Vec<_>>();

    let used_indexes = canonical_indexes
        .iter()
        .filter_map(|positions| positions.first().copied().filter(|_| positions.len() == 1))
        .collect::<Vec<_>>();

    let unused_headers = header_matches
        .iter()
        .filter(|entry| !used_indexes.contains(&entry.incoming_index))
        .map(|entry| entry.incoming_header.clone())
        .collect::<Vec<_>>();

    let can_validate = duplicate_columns.is_empty() && unresolved_required.is_empty();
    let alias_mapped_required_columns = if can_validate {
        let mut columns = Vec::new();

        for i in 0..REQUIRED_COLUMNS.len() {
            if let Some(index) = index_map[i] {
                if let Some(match_entry) = header_matches.iter().find(|entry| {
                    entry.incoming_index == index
                        && entry.match_quality == Some(HeaderMatchQuality::Alias)
                }) {
                    columns.push(
                        match_entry
                            .canonical_column
                            .as_ref()
                            .cloned()
                            .unwrap_or_else(|| REQUIRED_COLUMNS[i].to_string()),
                    );
                }
            }
        }

        columns
    } else {
        Vec::new()
    };
    let safe_to_auto_apply = can_validate && alias_mapped_required_columns.is_empty();

    let header_indexes = if can_validate {
        Some([
            index_map[0].expect("parent_sku must exist"),
            index_map[1].expect("sku must exist"),
            index_map[2].expect("jan must exist"),
            index_map[3].expect("qty must exist"),
            index_map[4].expect("brand must exist"),
            index_map[5].expect("template must exist"),
            index_map[6].expect("printer_profile must exist"),
            index_map[7].expect("enabled must exist"),
        ])
    } else {
        None
    };
    HeaderResolution {
        header_matches,
        unused_headers,
        unresolved_required,
        duplicate_columns,
        alias_mapped_required_columns,
        safe_to_auto_apply,
        can_validate,
        header_indexes,
    }
}

pub fn validate_headers(headers: &[&str]) -> Result<(), HeaderError> {
    resolve_header_indexes(headers).map(|_| ())
}

pub fn validate_rows_with_headers(
    headers: &[&str],
    rows: &[&[&str]],
    first_data_row_number: usize,
) -> Result<BatchValidationResult, HeaderError> {
    let header_indexes = resolve_header_indexes(headers)?;
    let max_index = header_indexes.iter().copied().max().unwrap_or(0);

    let mut row_results = Vec::with_capacity(rows.len());

    for (offset, values) in rows.iter().enumerate() {
        let row_number = first_data_row_number + offset;

        if values.len() <= max_index {
            row_results.push(BatchRowResult::Invalid {
                row_number,
                error: RowError::InvalidColumnCount {
                    row_number,
                    expected: REQUIRED_COLUMNS.len(),
                    actual: values.len(),
                },
            });
            continue;
        }

        let ordered_values = [
            values[header_indexes[0]],
            values[header_indexes[1]],
            values[header_indexes[2]],
            values[header_indexes[3]],
            values[header_indexes[4]],
            values[header_indexes[5]],
            values[header_indexes[6]],
            values[header_indexes[7]],
        ];

        match validate_row(row_number, &ordered_values) {
            Ok(row) => row_results.push(BatchRowResult::Valid { row_number, row }),
            Err(error) => row_results.push(BatchRowResult::Invalid { row_number, error }),
        }
    }

    Ok(BatchValidationResult {
        header_indexes,
        row_results,
    })
}

pub fn validate_row_with_headers(
    row_number: usize,
    headers: &[&str],
    values: &[&str],
) -> Result<ImportRow, RowError> {
    let index_map = resolve_header_indexes(headers).map_err(RowError::HeaderValidation)?;
    if values.len() < index_map.iter().copied().max().unwrap_or(0) + 1 {
        return Err(RowError::InvalidColumnCount {
            row_number,
            expected: REQUIRED_COLUMNS.len(),
            actual: values.len(),
        });
    }

    let row_values = index_map.iter().map(|&idx| values[idx]).collect::<Vec<_>>();

    validate_row(row_number, &row_values)
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
        value if value.eq_ignore_ascii_case("true") => Some(true),
        value if value.eq_ignore_ascii_case("false") => Some(false),
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
    use super::{
        inspect_header_resolution, resolve_header_indexes, validate_headers, validate_row,
        validate_row_with_headers, validate_rows_with_headers, BatchRowResult, CellError,
        HeaderError, HeaderMatchQuality, RowError,
    };

    #[test]
    fn inspect_reports_match_quality() {
        let headers = ["parent_sku", "sku", "Unknown", "Parent SKU", "Product SKU"];

        let resolution = inspect_header_resolution(&headers);
        assert_eq!(
            resolution.header_matches[0].match_quality,
            Some(HeaderMatchQuality::Exact)
        );
        assert_eq!(
            resolution.header_matches[1].match_quality,
            Some(HeaderMatchQuality::Exact)
        );
        assert_eq!(resolution.header_matches[2].match_quality, None);
        assert_eq!(
            resolution.header_matches[3].match_quality,
            Some(HeaderMatchQuality::Alias)
        );
        assert_eq!(
            resolution.header_matches[4].match_quality,
            Some(HeaderMatchQuality::Alias)
        );
        assert_eq!(
            resolution.unresolved_required,
            vec![
                "jan".to_string(),
                "qty".to_string(),
                "brand".to_string(),
                "template".to_string(),
                "printer_profile".to_string(),
                "enabled".to_string(),
            ]
        );
        assert_eq!(resolution.alias_mapped_required_columns.len(), 0);
        assert!(!resolution.safe_to_auto_apply);
    }

    #[test]
    fn accepts_japanese_business_alias_headers() {
        let headers = [
            "parent_sku",
            "商品コード",
            "JANコード",
            "印刷枚数",
            "brand",
            "ラベル名",
            "printer",
            "有効",
            "商品名",
        ];

        let resolution = inspect_header_resolution(&headers);

        assert!(resolution.can_validate);
        assert_eq!(resolution.header_indexes, Some([0, 1, 2, 3, 4, 5, 6, 7]));
        assert_eq!(resolution.unused_headers, vec!["商品名".to_string()]);
        assert_eq!(
            resolution.alias_mapped_required_columns,
            vec![
                "sku".to_string(),
                "jan".to_string(),
                "qty".to_string(),
                "template".to_string(),
                "printer_profile".to_string(),
                "enabled".to_string(),
            ]
        );
        assert!(!resolution.safe_to_auto_apply);
        assert_eq!(
            resolution.header_matches[1].match_quality,
            Some(HeaderMatchQuality::Alias)
        );
        assert_eq!(
            resolution.header_matches[2].match_quality,
            Some(HeaderMatchQuality::Alias)
        );
        assert_eq!(
            resolution.header_matches[3].match_quality,
            Some(HeaderMatchQuality::Alias)
        );
        assert_eq!(
            resolution.header_matches[7].match_quality,
            Some(HeaderMatchQuality::Alias)
        );
    }

    #[test]
    fn inspect_flags_required_column_gaps_with_alias_headers() {
        let headers = [
            "商品コード",
            "JANコード",
            "入数",
            "ブランド",
            "ラベル名",
            "有効",
        ];

        let resolution = inspect_header_resolution(&headers);
        assert!(!resolution.can_validate);
        assert_eq!(
            resolution.unresolved_required,
            vec!["parent_sku".to_string(), "printer_profile".to_string(),]
        );
        assert_eq!(
            resolution.alias_mapped_required_columns,
            Vec::<String>::new()
        );
        assert!(resolution.duplicate_columns.is_empty());
        assert!(!resolution.safe_to_auto_apply);
    }

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

        let resolution = inspect_header_resolution(&headers);
        assert!(resolution.can_validate);
        assert!(resolution.safe_to_auto_apply);
        assert_eq!(
            resolution.alias_mapped_required_columns,
            Vec::<String>::new()
        );
    }

    #[test]
    fn inspect_reports_matched_and_unused_headers() {
        let headers = [
            "Parent SKU",
            "SKU",
            "JAN Code",
            "Quantity",
            "Brand",
            "Template",
            "Printer",
            "Active",
            "Notes",
        ];

        let resolution = inspect_header_resolution(&headers);

        assert!(resolution.can_validate);
        assert_eq!(resolution.unresolved_required.len(), 0);
        assert_eq!(resolution.duplicate_columns.len(), 0);
        assert_eq!(resolution.unused_headers, vec!["Notes".to_string()]);
        assert_eq!(resolution.header_indexes, Some([0, 1, 2, 3, 4, 5, 6, 7]));
        assert_eq!(resolution.header_matches.len(), 9);
        assert_eq!(
            resolution.header_matches[0].canonical_column,
            Some("parent_sku".to_string())
        );
        assert_eq!(
            resolution.header_matches[5].canonical_column,
            Some("template".to_string())
        );
    }

    #[test]
    fn inspect_reports_unresolved_required_columns() {
        let headers = ["parent_sku", "sku", "jan", "qty", "brand"];
        let resolution = inspect_header_resolution(&headers);

        assert!(!resolution.can_validate);
        assert_eq!(
            resolution.unresolved_required,
            vec![
                "template".to_string(),
                "printer_profile".to_string(),
                "enabled".to_string(),
            ]
        );
        assert!(resolution.duplicate_columns.is_empty());
        assert_eq!(resolution.header_indexes, None);
        assert_eq!(resolution.unused_headers, Vec::<String>::new());
    }

    #[test]
    fn inspect_reports_duplicates_and_excludes_ambiguous_headers_from_validation() {
        let headers = [
            "SKU",
            "item_sku",
            "parent_sku",
            "jan",
            "qty",
            "brand",
            "template",
            "printer_profile",
            "enabled",
            "comment",
        ];

        let resolution = inspect_header_resolution(&headers);
        assert!(!resolution.can_validate);
        assert_eq!(resolution.duplicate_columns, vec!["sku".to_string()]);
        assert_eq!(
            resolution.unused_headers,
            vec![
                "SKU".to_string(),
                "item_sku".to_string(),
                "comment".to_string()
            ]
        );
        assert_eq!(resolution.header_indexes, None);
    }

    #[test]
    fn rejects_japanese_duplicate_alias_columns() {
        let headers = [
            "商品コード",
            "品番",
            "JANコード",
            "印刷枚数",
            "ブランド",
            "テンプレート名",
            "printer",
            "有効",
            "parent_sku",
            "printer_profile",
        ];

        let resolution = inspect_header_resolution(&headers);
        assert!(!resolution.can_validate);
        assert_eq!(
            resolution.duplicate_columns,
            vec!["sku".to_string(), "printer_profile".to_string()]
        );
        assert_eq!(resolution.header_indexes, None);
        assert_eq!(
            resolution.alias_mapped_required_columns,
            Vec::<String>::new()
        );
    }

    #[test]
    fn accepts_alias_headers_with_case_and_punctuation() {
        let headers = [
            "Parent SKU",
            "product_sku",
            "JAN CODE",
            "Print Qty",
            "Brand Name",
            "Template Name",
            "Printer Profile",
            "Active",
        ];

        let resolved = resolve_header_indexes(&headers).expect("alias headers should resolve");
        assert_eq!(resolved, [0, 1, 2, 3, 4, 5, 6, 7]);
    }

    #[test]
    fn rejects_ambiguous_headers() {
        let headers = [
            "SKU",
            "item_sku",
            "parent_sku",
            "jan",
            "qty",
            "brand",
            "template",
            "printer_profile",
            "enabled",
        ];

        let err = resolve_header_indexes(&headers).expect_err("duplicates should fail");
        assert_eq!(err, HeaderError::DuplicateColumns(vec!["sku".to_string()]));
    }

    #[test]
    fn maps_shuffled_alias_headers_when_validating_rows() {
        let headers = [
            "Enabled",
            "SKU",
            "Jan Code",
            "Parent SKU",
            "Template",
            "Brand Name",
            "Printer",
            "Quantity",
        ];

        let values = [
            "TRUE",
            "SKU-100",
            "4006381333931",
            "PARENT-100",
            "basic-50x30",
            "Acme",
            "pdf-a4-proof",
            "24",
        ];

        let row = validate_row_with_headers(2, &headers, &values)
            .expect("alias and shuffled headers should work");
        assert_eq!(row.parent_sku, "PARENT-100");
        assert_eq!(row.sku, "SKU-100");
        assert_eq!(row.jan.as_str(), "4006381333931");
        assert_eq!(row.qty, 24);
        assert_eq!(row.brand, "Acme");
        assert_eq!(row.template, "basic-50x30");
        assert_eq!(row.printer_profile, "pdf-a4-proof");
        assert!(row.enabled);
    }

    #[test]
    fn validates_row_batch_with_single_header_resolution() {
        let headers = [
            "Enabled",
            "Item SKU",
            "JAN Code",
            "Parent SKU",
            "Template",
            "Brand Name",
            "Printer",
            "Quantity",
        ];

        let rows: Vec<&[&str]> = vec![
            &[
                "TRUE",
                "SKU-100",
                "4006381333931",
                "PARENT-100",
                "basic-50x30",
                "Acme",
                "pdf-a4-proof",
                "24",
            ],
            &["FALSE", "", "", "", "", "", "", ""],
        ];

        let result = validate_rows_with_headers(&headers, &rows, 2)
            .expect("batch validation should complete");

        assert_eq!(result.row_results.len(), 2);
        assert_eq!(
            result.row_results[0],
            BatchRowResult::Valid {
                row_number: 2,
                row: validate_row(
                    2,
                    &[
                        "PARENT-100",
                        "SKU-100",
                        "4006381333931",
                        "24",
                        "Acme",
                        "basic-50x30",
                        "pdf-a4-proof",
                        "TRUE"
                    ]
                )
                .expect("fixture row should pass"),
            }
        );
        assert!(matches!(
            &result.row_results[1],
            BatchRowResult::Invalid { row_number: 3, .. }
        ));
    }

    #[test]
    fn validates_batch_even_with_extra_columns() {
        let headers = [
            "parent_sku",
            "sku",
            "jan",
            "qty",
            "brand",
            "template",
            "printer_profile",
            "enabled",
            "memo",
        ];

        let rows: Vec<&[&str]> = vec![&[
            "PARENT-200",
            "SKU-200",
            "4006381333931",
            "2",
            "Acme",
            "basic-50x30",
            "pdf-a4-proof",
            "TRUE",
            "extra-column-ignored",
        ]];

        let result = validate_rows_with_headers(&headers, &rows, 2)
            .expect("extra columns should not prevent required validation");

        assert!(matches!(
            &result.row_results[0],
            BatchRowResult::Valid { row_number: 2, .. }
        ));
    }

    #[test]
    fn batch_validation_rejects_missing_headers() {
        let headers = ["parent_sku", "sku", "jan"];
        let rows: Vec<&[&str]> = vec![&["PARENT-0001", "SKU-0001", "4006381333931"]];

        let err = validate_rows_with_headers(&headers, &rows, 2)
            .expect_err("missing headers should fail");
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
    fn preserves_row_numbering_for_dataset() {
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

        let rows: Vec<&[&str]> = vec![
            &[
                "PARENT-0001",
                "SKU-0001",
                "4006381333931",
                "24",
                "Acme",
                "basic-50x30",
                "pdf-a4-proof",
                "TRUE",
            ],
            &[
                "PARENT-0002",
                "SKU-0002",
                "4006381333931",
                "1",
                "Acme",
                "basic-50x30",
                "pdf-a4-proof",
                "FALSE",
            ],
        ];

        let result = validate_rows_with_headers(&headers, &rows, 5)
            .expect("batch with explicit start row should pass");

        let first_row_number = match &result.row_results[0] {
            BatchRowResult::Valid { row_number, .. } => *row_number,
            BatchRowResult::Invalid { row_number, .. } => *row_number,
        };
        let second_row_number = match &result.row_results[1] {
            BatchRowResult::Valid { row_number, .. } => *row_number,
            BatchRowResult::Invalid { row_number, .. } => *row_number,
        };

        assert_eq!(first_row_number, 5);
        assert_eq!(second_row_number, 6);
    }

    #[test]
    fn rejects_too_short_row_for_required_headers() {
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

        let values = ["PARENT-0001", "SKU-0001", "4006381333931", "24"];

        let err =
            validate_row_with_headers(3, &headers, &values).expect_err("short rows should fail");
        assert_eq!(
            err,
            RowError::InvalidColumnCount {
                row_number: 3,
                expected: 8,
                actual: 4,
            }
        );
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
    fn resolves_business_alias_headers_from_fixture() {
        let (header, row) = fixture_row(include_str!(
            "../../../packages/fixtures/importer/catalog-valid-business-headers.csv"
        ));
        let headers = header.iter().map(String::as_str).collect::<Vec<_>>();

        let resolution = inspect_header_resolution(&headers);
        assert!(resolution.can_validate);
        assert_eq!(resolution.unused_headers, vec!["商品名".to_string()]);
        assert_eq!(
            resolution.alias_mapped_required_columns,
            vec![
                "parent_sku".to_string(),
                "sku".to_string(),
                "jan".to_string(),
                "qty".to_string(),
                "brand".to_string(),
                "template".to_string(),
                "printer_profile".to_string(),
                "enabled".to_string(),
            ]
        );
        assert!(!resolution.safe_to_auto_apply);

        let row = validate_row_with_headers(2, &headers, &row).expect("fixture row should pass");
        assert_eq!(row.parent_sku, "PARENT-BIZ-0001");
        assert_eq!(row.sku, "SKU-BIZ-0001");
        assert_eq!(row.jan.as_str(), "4006381333931");
        assert_eq!(row.qty, 24);
        assert_eq!(row.brand, "Acme");
        assert_eq!(row.template, "basic-50x30");
        assert_eq!(row.printer_profile, "pdf-a4-proof");
        assert!(row.enabled);
    }

    #[test]
    fn rejects_ambiguous_business_headers_from_fixture() {
        let (header, row) = fixture_row(include_str!(
            "../../../packages/fixtures/importer/catalog-invalid-ambiguous-headers.csv"
        ));
        let headers = header.iter().map(String::as_str).collect::<Vec<_>>();

        let resolution = inspect_header_resolution(&headers);
        assert!(!resolution.can_validate);
        assert_eq!(resolution.duplicate_columns, vec!["sku".to_string()]);

        let err = validate_rows_with_headers(&headers, &[row.as_slice()], 2)
            .expect_err("ambiguous headers should fail validation");
        assert_eq!(err, HeaderError::DuplicateColumns(vec!["sku".to_string()]));
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
    fn accepts_upper_case_enabled_values() {
        let row = [
            "PARENT-0001",
            "SKU-0001",
            "4006381333931",
            "24",
            "Acme",
            "basic-50x30",
            "pdf-a4-proof",
            "TRUE",
        ];

        let row = validate_row(2, &row).expect("uppercase booleans should be accepted");

        assert!(row.enabled);
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
