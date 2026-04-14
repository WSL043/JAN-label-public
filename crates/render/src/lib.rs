const LABEL_WIDTH_MM: f64 = 50.0;
const LABEL_HEIGHT_MM: f64 = 30.0;

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct RenderLabelRequest {
    pub job_id: String,
    pub sku: String,
    pub brand: String,
    pub jan: String,
    pub qty: u32,
    pub template_version: String,
}

pub fn render_svg(request: &RenderLabelRequest) -> String {
    format!(
        "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"50mm\" height=\"30mm\" viewBox=\"0 0 50 30\">\n  <rect width=\"50\" height=\"30\" fill=\"#ffffff\" stroke=\"#000000\" stroke-width=\"0.2\" />\n  <text x=\"2\" y=\"5\" font-size=\"3\">job:{}</text>\n  <text x=\"2\" y=\"10\" font-size=\"4\">brand:{}</text>\n  <text x=\"2\" y=\"15\" font-size=\"4\">sku:{}</text>\n  <text x=\"2\" y=\"20\" font-size=\"4\">jan:{}</text>\n  <text x=\"2\" y=\"25\" font-size=\"4\">qty:{}</text>\n  <text x=\"30\" y=\"28\" font-size=\"2\">template:{}</text>\n</svg>\n",
        request.job_id,
        request.brand,
        request.sku,
        request.jan,
        request.qty,
        request.template_version
    )
}

pub fn render_pdf(request: &RenderLabelRequest) -> Vec<u8> {
    let width_pt = mm_to_points(LABEL_WIDTH_MM);
    let height_pt = mm_to_points(LABEL_HEIGHT_MM);
    let content_stream = render_pdf_content(request, height_pt);
    let objects = [
        "<< /Type /Catalog /Pages 2 0 R >>".to_string(),
        "<< /Type /Pages /Count 1 /Kids [3 0 R] >>".to_string(),
        format!(
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {} {}] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>",
            format_pdf_number(width_pt),
            format_pdf_number(height_pt)
        ),
        format!(
            "<< /Length {} >>\nstream\n{}endstream",
            content_stream.len(),
            content_stream
        ),
        "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>".to_string(),
    ];

    let mut pdf = String::from("%PDF-1.4\n");
    let mut offsets = Vec::with_capacity(objects.len() + 1);
    offsets.push(0);

    for (index, object) in objects.iter().enumerate() {
        offsets.push(pdf.len());
        pdf.push_str(&format!("{} 0 obj\n{}\nendobj\n", index + 1, object));
    }

    let xref_start = pdf.len();
    pdf.push_str(&format!(
        "xref\n0 {}\n0000000000 65535 f \n",
        objects.len() + 1
    ));
    for offset in offsets.iter().skip(1) {
        pdf.push_str(&format!("{offset:010} 00000 n \n"));
    }
    pdf.push_str(&format!(
        "trailer\n<< /Size {} /Root 1 0 R >>\nstartxref\n{}\n%%EOF\n",
        objects.len() + 1,
        xref_start
    ));

    pdf.into_bytes()
}

fn render_pdf_content(request: &RenderLabelRequest, height_pt: f64) -> String {
    [
        "1 1 1 rg".to_string(),
        format!(
            "0 0 {} {} re f",
            format_pdf_number(mm_to_points(LABEL_WIDTH_MM)),
            format_pdf_number(height_pt)
        ),
        "0 0 0 RG".to_string(),
        format!("{} w", format_pdf_number(mm_to_points(0.2))),
        format!(
            "0 0 {} {} re S",
            format_pdf_number(mm_to_points(LABEL_WIDTH_MM)),
            format_pdf_number(height_pt)
        ),
        pdf_text(2.0, 5.0, 3.0, &format!("job:{}", request.job_id), height_pt),
        pdf_text(
            2.0,
            10.0,
            4.0,
            &format!("brand:{}", request.brand),
            height_pt,
        ),
        pdf_text(2.0, 15.0, 4.0, &format!("sku:{}", request.sku), height_pt),
        pdf_text(2.0, 20.0, 4.0, &format!("jan:{}", request.jan), height_pt),
        pdf_text(2.0, 25.0, 4.0, &format!("qty:{}", request.qty), height_pt),
        pdf_text(
            30.0,
            28.0,
            2.0,
            &format!("template:{}", request.template_version),
            height_pt,
        ),
    ]
    .join("\n")
        + "\n"
}

fn pdf_text(x_mm: f64, y_mm: f64, font_size_mm: f64, text: &str, height_pt: f64) -> String {
    format!(
        "BT\n/F1 {} Tf\n{} {} Td\n({}) Tj\nET",
        format_pdf_number(mm_to_points(font_size_mm)),
        format_pdf_number(mm_to_points(x_mm)),
        format_pdf_number(height_pt - mm_to_points(y_mm)),
        escape_pdf_text(text)
    )
}

fn escape_pdf_text(text: &str) -> String {
    text.replace('\\', "\\\\")
        .replace('(', "\\(")
        .replace(')', "\\)")
}

fn mm_to_points(value_mm: f64) -> f64 {
    value_mm * 72.0 / 25.4
}

fn format_pdf_number(value: f64) -> String {
    format!("{value:.3}")
}

#[cfg(test)]
mod tests {
    use super::{render_pdf, render_svg, RenderLabelRequest};

    #[test]
    fn svg_matches_golden_fixture() {
        let request = sample_request();

        let actual = render_svg(&request);
        let expected = include_str!("../../../packages/fixtures/golden/basic-label.svg");
        assert_eq!(actual.trim_end(), expected.trim_end());
    }

    #[test]
    fn pdf_matches_golden_fixture() {
        let request = sample_request();

        let actual = render_pdf(&request);
        let expected = include_bytes!("../../../packages/fixtures/golden/basic-label.pdf");
        assert_eq!(actual.as_slice(), expected);
    }

    #[test]
    fn pdf_escapes_reserved_text_characters() {
        let request = RenderLabelRequest {
            brand: r"Acme (North)\West".to_string(),
            ..sample_request()
        };

        let actual = String::from_utf8(render_pdf(&request)).expect("pdf bytes should stay ascii");
        assert!(actual.contains(r"(brand:Acme \(North\)\\West)"));
    }

    fn sample_request() -> RenderLabelRequest {
        RenderLabelRequest {
            job_id: "JOB-20260414-0001".to_string(),
            sku: "SKU-0001".to_string(),
            brand: "Acme".to_string(),
            jan: "4006381333931".to_string(),
            qty: 24,
            template_version: "basic-50x30@v1".to_string(),
        }
    }
}
