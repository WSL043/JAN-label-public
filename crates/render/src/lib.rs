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

#[cfg(test)]
mod tests {
    use super::{RenderLabelRequest, render_svg};

    #[test]
    fn svg_matches_golden_fixture() {
        let request = RenderLabelRequest {
            job_id: "JOB-20260414-0001".to_string(),
            sku: "SKU-0001".to_string(),
            brand: "Acme".to_string(),
            jan: "4006381333931".to_string(),
            qty: 24,
            template_version: "basic-50x30@v1".to_string(),
        };

        let actual = render_svg(&request);
        let expected = include_str!("../../../packages/fixtures/golden/basic-label.svg");
        assert_eq!(actual, expected);
    }
}

