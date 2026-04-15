use serde::Deserialize;
use std::fmt;
use std::fs;
use std::path::{Component, Path};

use include_dir::{include_dir, Dir};

static TEMPLATES_DIR: Dir<'static> = include_dir!("$CARGO_MANIFEST_DIR/../../packages/templates");

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct RenderLabelRequest {
    pub job_id: String,
    pub sku: String,
    pub brand: String,
    pub jan: String,
    pub qty: u32,
    pub template_version: String,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum TemplateResolutionError {
    UnknownTemplateVersion {
        requested: String,
        known_versions: Vec<String>,
    },
    MalformedTemplateSpec {
        template_version: String,
        source: String,
        message: String,
    },
}

#[derive(Debug, Clone, Deserialize)]
pub struct TemplateManifest {
    pub schema_version: String,
    pub default_template_version: String,
    pub templates: Vec<TemplateManifestEntry>,
}

#[derive(Debug, Clone, PartialEq, Eq, Deserialize)]
pub struct TemplateManifestEntry {
    pub version: String,
    pub path: String,
    pub label_name: String,
    #[serde(default = "default_manifest_entry_enabled")]
    pub enabled: bool,
    pub description: Option<String>,
}

impl fmt::Display for TemplateResolutionError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::UnknownTemplateVersion {
                requested,
                known_versions,
            } => {
                write!(
                    f,
                    "unknown template_version='{}', known versions: {:?}",
                    requested, known_versions
                )
            }
            Self::MalformedTemplateSpec {
                template_version,
                source,
                message,
            } => {
                write!(
                    f,
                    "template '{}' failed to parse from '{}': {}",
                    template_version, source, message
                )
            }
        }
    }
}

impl std::error::Error for TemplateResolutionError {}

#[derive(Debug, Clone, Deserialize)]
#[allow(dead_code)]
pub struct LabelTemplate {
    pub schema_version: String,
    pub template_version: String,
    pub label_name: Option<String>,
    pub description: Option<String>,
    pub page: LabelTemplatePage,
    #[serde(default)]
    pub border: Option<LabelTemplateBorder>,
    pub fields: Vec<LabelTemplateField>,
}

#[derive(Debug, Clone, Deserialize)]
pub struct LabelTemplatePage {
    pub width_mm: f64,
    pub height_mm: f64,
    pub background_fill: Option<String>,
}

#[derive(Debug, Clone, Deserialize)]
pub struct LabelTemplateBorder {
    #[serde(default = "default_border_visible")]
    pub visible: bool,
    pub color: Option<String>,
    #[serde(default)]
    pub width_mm: f64,
}

#[derive(Debug, Clone, Deserialize)]
#[allow(dead_code)]
pub struct LabelTemplateField {
    pub name: String,
    pub x_mm: f64,
    pub y_mm: f64,
    pub font_size_mm: f64,
    pub template: String,
    #[allow(dead_code)]
    pub color: Option<String>,
}

pub fn render_svg(request: &RenderLabelRequest) -> Result<String, TemplateResolutionError> {
    render_svg_with_overlay(request, None)
}

pub fn render_svg_with_overlay(
    request: &RenderLabelRequest,
    local_manifest_path: Option<&Path>,
) -> Result<String, TemplateResolutionError> {
    let template = resolve_template_with_overlay(&request.template_version, local_manifest_path)?;
    Ok(render_svg_with_template(request, &template))
}

pub fn render_svg_with_template(request: &RenderLabelRequest, template: &LabelTemplate) -> String {
    let width = template.page.width_mm;
    let height = template.page.height_mm;
    let border = template.border.as_ref().filter(|value| value.visible);
    let border_color =
        sanitize_svg_color(border.and_then(|value| value.color.as_deref()), "#000000");
    let background_fill = sanitize_svg_color(template.page.background_fill.as_deref(), "#ffffff");
    let border_width = border.map(|value| value.width_mm).unwrap_or(0.2);
    let mut svg = String::new();
    svg.push_str(&format!(
        "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{width}mm\" height=\"{height}mm\" viewBox=\"0 0 {width} {height}\">\n",
        width = trim_mm(width),
        height = trim_mm(height)
    ));
    svg.push_str(&format!(
        "  <rect width=\"{width}\" height=\"{height}\" fill=\"{fill}\" stroke=\"{stroke}\" stroke-width=\"{stroke_width}\" />\n",
        width = trim_mm(width),
        height = trim_mm(height),
        fill = escape_xml_attribute(&background_fill),
        stroke = escape_xml_attribute(if border.is_some() {
            border_color.as_str()
        } else {
            "none"
        }),
        stroke_width = trim_mm(border_width)
    ));

    for field in template.fields.iter() {
        let field_color = sanitize_svg_color(field.color.as_deref(), "#000000");
        svg.push_str(&format!(
            "  <text x=\"{}\" y=\"{}\" font-size=\"{}\" fill=\"{}\">{}</text>\n",
            trim_mm(field.x_mm),
            trim_mm(field.y_mm),
            trim_mm(field.font_size_mm),
            escape_xml_attribute(&field_color),
            escape_xml_text(&render_field_text(&field.template, request))
        ));
    }

    svg.push_str("</svg>\n");
    svg
}

pub fn render_pdf(request: &RenderLabelRequest) -> Result<Vec<u8>, TemplateResolutionError> {
    render_pdf_with_overlay(request, None)
}

pub fn render_pdf_with_overlay(
    request: &RenderLabelRequest,
    local_manifest_path: Option<&Path>,
) -> Result<Vec<u8>, TemplateResolutionError> {
    let template = resolve_template_with_overlay(&request.template_version, local_manifest_path)?;
    Ok(render_pdf_with_template(request, &template))
}

pub fn render_pdf_with_template(request: &RenderLabelRequest, template: &LabelTemplate) -> Vec<u8> {
    let width_pt = mm_to_points(template.page.width_mm);
    let height_pt = mm_to_points(template.page.height_mm);
    let content_stream = render_pdf_content(request, template, height_pt);
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

pub fn parse_template_source(
    source: &str,
    source_name: &str,
) -> Result<LabelTemplate, TemplateResolutionError> {
    serde_json::from_str(source).map_err(|error| TemplateResolutionError::MalformedTemplateSpec {
        template_version: template_version_hint_from_source(source),
        source: source_name.to_string(),
        message: error.to_string(),
    })
}

pub fn template_catalog() -> Result<TemplateManifest, TemplateResolutionError> {
    let manifest_source = "template-manifest.json";
    let manifest_contents = template_file_contents(manifest_source).map_err(|error| {
        TemplateResolutionError::MalformedTemplateSpec {
            template_version: String::from("<manifest>"),
            source: manifest_source.to_string(),
            message: error.to_string(),
        }
    })?;
    parse_template_catalog_from_str(&manifest_contents, manifest_source)
}

pub fn template_catalog_with_overlay(
    local_manifest_path: Option<&Path>,
) -> Result<TemplateManifest, TemplateResolutionError> {
    let packaged_manifest = template_catalog()?;
    let Some(local_manifest_path) = local_manifest_path else {
        return Ok(packaged_manifest);
    };

    let manifest_source = local_manifest_path.display().to_string();
    let local_manifest_contents = fs::read_to_string(local_manifest_path).map_err(|error| {
        TemplateResolutionError::MalformedTemplateSpec {
            template_version: String::from("<manifest>"),
            source: manifest_source.clone(),
            message: format!("failed to read local template catalog: {error}"),
        }
    })?;
    let local_manifest =
        parse_template_catalog_from_str(&local_manifest_contents, &manifest_source)?;

    Ok(merge_template_manifests(packaged_manifest, local_manifest))
}

fn parse_template_catalog_from_str(
    source: &str,
    manifest_source: &str,
) -> Result<TemplateManifest, TemplateResolutionError> {
    serde_json::from_str(source).map_err(|error| TemplateResolutionError::MalformedTemplateSpec {
        template_version: String::from("<manifest>"),
        source: manifest_source.to_string(),
        message: error.to_string(),
    })
}

pub fn resolve_template(template_version: &str) -> Result<LabelTemplate, TemplateResolutionError> {
    resolve_template_with_overlay(template_version, None)
}

pub fn resolve_template_with_overlay(
    template_version: &str,
    local_manifest_path: Option<&Path>,
) -> Result<LabelTemplate, TemplateResolutionError> {
    let manifest = template_catalog_with_overlay(local_manifest_path)?;
    resolve_template_from_manifest(template_version, &manifest, local_manifest_path)
}

fn resolve_template_from_manifest(
    template_version: &str,
    manifest: &TemplateManifest,
    local_manifest_path: Option<&Path>,
) -> Result<LabelTemplate, TemplateResolutionError> {
    let entry = find_template_entry(template_version, manifest)?;
    let source = entry.path.clone();
    let template =
        template_file_contents_with_overlay(&source, local_manifest_path).map_err(|error| {
            TemplateResolutionError::MalformedTemplateSpec {
                template_version: template_version.to_string(),
                source: source.clone(),
                message: error,
            }
        })?;

    serde_json::from_str(&template).map_err(|error| {
        TemplateResolutionError::MalformedTemplateSpec {
            template_version: template_version.to_string(),
            source,
            message: error.to_string(),
        }
    })
}

fn template_file_contents(path: &str) -> Result<String, String> {
    if Path::new(path).is_absolute() {
        return Err("template path must be a relative path".to_string());
    }

    let normalized_path = path.replace('\\', "/");
    TEMPLATES_DIR
        .get_file(normalized_path.as_str())
        .and_then(|file| file.contents_utf8())
        .map(ToString::to_string)
        .ok_or_else(|| format!("template file was not found in embedded templates: {path}"))
}

fn template_file_contents_with_overlay(
    path: &str,
    local_manifest_path: Option<&Path>,
) -> Result<String, String> {
    let normalized_path = path.replace('\\', "/");
    if Path::new(&normalized_path).is_absolute() {
        return Err("template path must be a relative path".to_string());
    }

    if let Some(manifest_parent) = local_manifest_path.and_then(|value| value.parent()) {
        if contains_parent_traversal(&normalized_path) {
            return Err(format!(
                "template path contains parent traversal and is rejected: {path}"
            ));
        }

        let local_template_path = manifest_parent.join(&normalized_path);
        if local_template_path.exists() {
            return fs::read_to_string(&local_template_path).map_err(|error| {
                format!(
                    "failed to read local template file '{}': {error}",
                    local_template_path.display()
                )
            });
        }
    }

    template_file_contents(&normalized_path)
}

fn merge_template_manifests(
    packaged: TemplateManifest,
    local: TemplateManifest,
) -> TemplateManifest {
    let mut merged_templates = Vec::new();
    let mut overlayed_versions = std::collections::HashSet::new();
    let mut local_templates_by_version = std::collections::HashMap::new();

    for local_entry in local.templates.iter() {
        local_templates_by_version.insert(local_entry.version.clone(), local_entry.clone());
    }

    for packaged_entry in packaged.templates.into_iter() {
        let merged_entry = local_templates_by_version
            .remove(&packaged_entry.version)
            .unwrap_or(packaged_entry);
        let merged_version = merged_entry.version.clone();
        merged_templates.push(merged_entry);
        overlayed_versions.insert(merged_version);
    }

    for local_entry in local.templates.into_iter() {
        if !overlayed_versions.contains(&local_entry.version) {
            merged_templates.push(local_entry);
        }
    }

    TemplateManifest {
        schema_version: packaged.schema_version,
        default_template_version: if local.default_template_version.trim().is_empty() {
            packaged.default_template_version
        } else {
            local.default_template_version
        },
        templates: merged_templates,
    }
}

fn contains_parent_traversal(path: &str) -> bool {
    Path::new(path)
        .components()
        .any(|value| matches!(value, Component::ParentDir))
}

fn find_template_entry(
    template_version: &str,
    manifest: &TemplateManifest,
) -> Result<TemplateManifestEntry, TemplateResolutionError> {
    manifest
        .templates
        .iter()
        .find(|value| value.enabled && value.version == template_version)
        .cloned()
        .ok_or_else(|| TemplateResolutionError::UnknownTemplateVersion {
            requested: template_version.to_string(),
            known_versions: manifest_templates(manifest),
        })
}

fn manifest_templates(manifest: &TemplateManifest) -> Vec<String> {
    manifest
        .templates
        .iter()
        .filter(|value| value.enabled)
        .map(|value| value.version.clone())
        .collect()
}

fn render_pdf_content(
    request: &RenderLabelRequest,
    template: &LabelTemplate,
    height_pt: f64,
) -> String {
    let mut lines = Vec::with_capacity(template.fields.len() + 6);
    let background_fill = sanitize_svg_color(template.page.background_fill.as_deref(), "#ffffff");
    let border = template.border.as_ref().filter(|value| value.visible);
    let border_width = border.map(|value| value.width_mm).unwrap_or(0.2);
    let border_color =
        sanitize_svg_color(border.and_then(|value| value.color.as_deref()), "#000000");
    let page_width = mm_to_points(template.page.width_mm);
    let page_height = height_pt;

    lines.push(pdf_fill_color(&background_fill));
    lines.push(format!(
        "0 0 {} {} re f",
        format_pdf_number(page_width),
        format_pdf_number(page_height)
    ));
    if border.is_some() {
        lines.push(pdf_stroke_color(&border_color));
        lines.push(format!(
            "{} w",
            format_pdf_number(mm_to_points(border_width))
        ));
        lines.push(format!(
            "0 0 {} {} re S",
            format_pdf_number(page_width),
            format_pdf_number(page_height)
        ));
    }

    for field in template.fields.iter() {
        lines.push(pdf_text(
            field.x_mm,
            field.y_mm,
            field.font_size_mm,
            &render_field_text(&field.template, request),
            page_height,
            &sanitize_svg_color(field.color.as_deref(), "#000000"),
        ));
    }

    lines.join("\n") + "\n"
}

fn render_field_text(field_template: &str, request: &RenderLabelRequest) -> String {
    let mut rendered = field_template.replace("{job_id}", &request.job_id);
    rendered = rendered.replace("{sku}", &request.sku);
    rendered = rendered.replace("{brand}", &request.brand);
    rendered = rendered.replace("{jan}", &request.jan);
    rendered = rendered.replace("{qty}", &request.qty.to_string());
    rendered.replace("{template_version}", &request.template_version)
}

fn pdf_text(
    x_mm: f64,
    y_mm: f64,
    font_size_mm: f64,
    text: &str,
    height_pt: f64,
    color: &str,
) -> String {
    format!(
        "{}\nBT\n/F1 {} Tf\n{} {} Td\n({}) Tj\nET",
        pdf_fill_color(color),
        format_pdf_number(mm_to_points(font_size_mm)),
        format_pdf_number(mm_to_points(x_mm)),
        format_pdf_number(height_pt - mm_to_points(y_mm)),
        escape_pdf_text(text)
    )
}

fn escape_pdf_text(text: &str) -> String {
    let mut escaped = String::with_capacity(text.len());

    for c in text.chars() {
        match c {
            '\\' => escaped.push_str("\\\\"),
            '(' => escaped.push_str("\\("),
            ')' => escaped.push_str("\\)"),
            '\r' => escaped.push_str("\\r"),
            '\n' => escaped.push_str("\\n"),
            '\t' => escaped.push_str("\\t"),
            _ => escaped.push(c),
        }
    }

    escaped
}

fn escape_xml_text(text: &str) -> String {
    let mut escaped = String::with_capacity(text.len());

    for c in text.chars() {
        match c {
            '&' => escaped.push_str("&amp;"),
            '<' => escaped.push_str("&lt;"),
            '>' => escaped.push_str("&gt;"),
            '"' => escaped.push_str("&quot;"),
            '\'' => escaped.push_str("&apos;"),
            _ => escaped.push(c),
        }
    }

    escaped
}

fn escape_xml_attribute(text: &str) -> String {
    escape_xml_text(text)
}

fn sanitize_svg_color(value: Option<&str>, fallback: &str) -> String {
    let candidate = value.unwrap_or(fallback).trim();
    if is_hex_color(candidate) {
        candidate.to_string()
    } else {
        fallback.to_string()
    }
}

fn template_version_hint_from_source(source: &str) -> String {
    serde_json::from_str::<serde_json::Value>(source)
        .ok()
        .and_then(|value| {
            value
                .get("template_version")
                .and_then(|value| value.as_str())
                .map(str::to_string)
        })
        .unwrap_or_else(|| "<draft>".to_string())
}

fn is_hex_color(value: &str) -> bool {
    let bytes = value.as_bytes();
    if !matches!(bytes.len(), 4 | 7) || bytes.first() != Some(&b'#') {
        return false;
    }

    bytes[1..].iter().all(|byte| byte.is_ascii_hexdigit())
}

fn mm_to_points(value_mm: f64) -> f64 {
    value_mm * 72.0 / 25.4
}

fn pdf_fill_color(value: &str) -> String {
    let (r, g, b) = hex_color_to_rgb(value);
    format!(
        "{} {} {} rg",
        format_pdf_number(r),
        format_pdf_number(g),
        format_pdf_number(b)
    )
}

fn pdf_stroke_color(value: &str) -> String {
    let (r, g, b) = hex_color_to_rgb(value);
    format!(
        "{} {} {} RG",
        format_pdf_number(r),
        format_pdf_number(g),
        format_pdf_number(b)
    )
}

fn hex_color_to_rgb(value: &str) -> (f64, f64, f64) {
    let normalized = sanitize_svg_color(Some(value), "#000000");
    let expanded = if normalized.len() == 4 {
        let bytes = normalized.as_bytes();
        format!(
            "#{}{}{}{}{}{}",
            bytes[1] as char,
            bytes[1] as char,
            bytes[2] as char,
            bytes[2] as char,
            bytes[3] as char,
            bytes[3] as char
        )
    } else {
        normalized
    };

    let red = u8::from_str_radix(&expanded[1..3], 16).unwrap_or(0);
    let green = u8::from_str_radix(&expanded[3..5], 16).unwrap_or(0);
    let blue = u8::from_str_radix(&expanded[5..7], 16).unwrap_or(0);

    (
        f64::from(red) / 255.0,
        f64::from(green) / 255.0,
        f64::from(blue) / 255.0,
    )
}

fn format_pdf_number(value: f64) -> String {
    format!("{value:.3}")
}

fn trim_mm(value: f64) -> String {
    let trimmed = format!("{value:.3}")
        .trim_end_matches('0')
        .trim_end_matches('.')
        .to_string();

    if trimmed.is_empty() {
        "0".to_string()
    } else {
        trimmed
    }
}

fn default_border_visible() -> bool {
    true
}

fn default_manifest_entry_enabled() -> bool {
    true
}

#[cfg(test)]
mod tests {
    use super::{
        escape_pdf_text, escape_xml_attribute, format_pdf_number, is_hex_color, mm_to_points,
        parse_template_catalog_from_str, parse_template_source, pdf_fill_color, pdf_stroke_color,
        render_pdf, render_pdf_with_template, render_svg, render_svg_with_template,
        resolve_template, resolve_template_with_overlay, sanitize_svg_color, template_catalog,
        template_catalog_with_overlay, RenderLabelRequest, TemplateResolutionError,
    };
    use std::{
        fs,
        path::PathBuf,
        time::{SystemTime, UNIX_EPOCH},
    };

    #[test]
    fn svg_matches_golden_fixture() {
        let request = sample_request();

        let actual = render_svg(&request).expect("render_svg should resolve known template");
        let expected = include_str!("../../../packages/fixtures/golden/basic-label.svg");
        assert_eq!(actual.trim_end(), expected.trim_end());
    }

    #[test]
    fn pdf_matches_golden_fixture() {
        let request = sample_request();

        let actual = render_pdf(&request).expect("render_pdf should resolve known template");
        let expected = include_bytes!("../../../packages/fixtures/golden/basic-label.pdf");
        assert_eq!(actual.as_slice(), expected);
    }

    #[test]
    fn pdf_escapes_reserved_text_characters() {
        let request = RenderLabelRequest {
            brand: r"Acme (North)\West".to_string(),
            ..sample_request()
        };

        let actual = String::from_utf8(
            render_pdf(&request).expect("render_pdf should resolve known template"),
        )
        .expect("pdf bytes should stay ascii");
        assert!(actual.contains(r"(brand:Acme \(North\)\\West)"));
    }

    #[test]
    fn svg_color_validation_accepts_hex_only() {
        assert!(is_hex_color("#fff"));
        assert!(is_hex_color("#ffffff"));
        assert!(!is_hex_color("url(javascript:alert(1))"));
        assert!(!is_hex_color("\" onload=\"alert(1)"));
    }

    #[test]
    fn svg_color_sanitizer_falls_back_for_invalid_values() {
        assert_eq!(sanitize_svg_color(Some("#123abc"), "#ffffff"), "#123abc");
        assert_eq!(
            sanitize_svg_color(Some("url(javascript:alert(1))"), "#ffffff"),
            "#ffffff"
        );
    }

    #[test]
    fn svg_attribute_escape_escapes_quotes_and_angles() {
        let escaped = escape_xml_attribute("\"bad<attr>&");
        assert_eq!(escaped, "&quot;bad&lt;attr&gt;&amp;");
    }

    #[test]
    fn inline_template_preview_respects_border_visibility_and_field_color() {
        let template = parse_template_source(
            r##"
            {
              "schema_version": "template-spec-v1",
              "template_version": "inline-preview@v1",
              "label_name": "inline-preview",
              "page": { "width_mm": 40, "height_mm": 20, "background_fill": "#ffeecc" },
              "border": { "visible": false, "color": "#112233", "width_mm": 0.4 },
              "fields": [
                {
                  "name": "sku",
                  "x_mm": 2,
                  "y_mm": 5,
                  "font_size_mm": 3,
                  "template": "sku:{sku}",
                  "color": "#123abc"
                }
              ]
            }
            "##,
            "inline-preview.json",
        )
        .expect("inline template should parse");
        let request = RenderLabelRequest {
            template_version: template.template_version.clone(),
            ..sample_request()
        };

        let svg = render_svg_with_template(&request, &template);
        assert!(svg.contains("fill=\"#ffeecc\""));
        assert!(svg.contains("stroke=\"none\""));
        assert!(svg.contains("fill=\"#123abc\""));

        let pdf = String::from_utf8(render_pdf_with_template(&request, &template))
            .expect("pdf bytes should stay ascii");
        assert!(pdf.contains("1.000 0.933 0.800 rg"));
        assert!(pdf.contains("0.071 0.227 0.737 rg"));
        assert!(!pdf.contains("0 0 113.386 56.693 re S"));
    }

    #[test]
    fn parse_template_source_reports_source_name_for_invalid_json() {
        let error = parse_template_source("{", "broken-preview.json")
            .expect_err("invalid json should fail");

        match error {
            TemplateResolutionError::MalformedTemplateSpec {
                template_version,
                source,
                ..
            } => {
                assert_eq!(template_version, "<draft>");
                assert_eq!(source, "broken-preview.json");
            }
            _ => panic!("expected malformed template error"),
        }
    }

    #[test]
    fn pdf_structure_and_xref_are_deterministic() {
        let request = sample_request();
        let actual = render_pdf(&request).expect("render_pdf should resolve known template");
        let actual_text = String::from_utf8(actual.clone()).expect("pdf bytes should stay ascii");
        assert_eq!(&actual[..8], b"%PDF-1.4");
        assert!(actual_text.ends_with("%%EOF\n"));

        let xref_start = actual_text
            .find("startxref\n")
            .and_then(|idx| actual_text[idx + "startxref\n".len()..].lines().next())
            .and_then(|line| line.parse::<usize>().ok())
            .expect("startxref value should be numeric");
        let xref_offset = actual_text.find("xref\n").expect("xref section must exist");
        assert_eq!(
            xref_start, xref_offset,
            "startxref must point to the xref table offset"
        );

        let xref_section = actual_text[xref_offset..]
            .split("trailer")
            .next()
            .expect("xref section should end before trailer");
        let xref_header = xref_section
            .lines()
            .nth(1)
            .expect("xref header should exist");
        assert_eq!(xref_header, "0 6");
        let trailer_start = actual_text
            .find("trailer\n")
            .expect("trailer section should exist");
        let declared_size = actual_text[trailer_start..]
            .lines()
            .find(|line| line.contains("/Size"))
            .and_then(|line| {
                line.split_whitespace()
                    .find(|token| token.parse::<usize>().is_ok())
            })
            .and_then(|token| token.parse::<usize>().ok())
            .expect("trailer /Size should be present");
        assert_eq!(declared_size, 6);

        let xref_offsets = xref_section
            .lines()
            .filter(|line| line.chars().next().is_some_and(|c| c.is_ascii_digit()))
            .count();
        assert_eq!(xref_offsets, 7);
    }

    #[test]
    fn pdf_media_box_matches_template_dimensions() {
        let request = sample_request();
        let actual = String::from_utf8(
            render_pdf(&request).expect("render_pdf should resolve known template"),
        )
        .expect("pdf bytes should stay ascii");
        let candidates: Vec<_> = actual
            .lines()
            .filter(|line| line.contains("/Type /Page /"))
            .collect();
        assert!(
            !candidates.is_empty(),
            "no /Type /Page line found in rendered output"
        );
        let page_line = candidates[0];
        let template = resolve_template(&request.template_version)
            .expect("expected template to be resolvable");

        let media_box = parse_media_box_values(page_line).unwrap_or_else(|| {
            panic!(
                "media box values should be parseable from page object, page_line={page_line:?}"
            );
        });
        assert_eq!(media_box.len(), 4);
        assert!(approx_eq(media_box[0], 0.0, 0.0001) && approx_eq(media_box[1], 0.0, 0.0001));
        assert!(approx_eq(
            media_box[2],
            mm_to_points(template.page.width_mm),
            0.001
        ));
        assert!(approx_eq(
            media_box[3],
            mm_to_points(template.page.height_mm),
            0.001
        ));
    }

    #[test]
    fn pdf_stream_contains_expected_geometry_and_text_fields() {
        let request = sample_request();
        let actual = render_pdf(&request).expect("render_pdf should resolve known template");
        let actual_text = String::from_utf8(actual.clone()).expect("pdf bytes should stay ascii");
        let (content_bytes, _) =
            extract_stream_content(&actual).expect("stream content should be present");
        let content =
            String::from_utf8(content_bytes.clone()).expect("pdf stream content should stay ascii");
        let content_len_declared =
            extract_content_length(&actual_text).expect("content length should be available");
        let template = resolve_template(&request.template_version)
            .expect("expected template to be resolvable");
        assert_eq!(
            content.len(),
            content_len_declared,
            "content stream length should match /Length"
        );

        let width = format_pdf_number(mm_to_points(template.page.width_mm));
        let height = format_pdf_number(mm_to_points(template.page.height_mm));
        let border_width = template.border.as_ref().map(|b| b.width_mm).unwrap_or(0.2);
        assert!(content.contains(&format!("0 0 {} {} re f", width, height)));
        let border_color = template
            .border
            .as_ref()
            .and_then(|value| value.color.as_deref())
            .unwrap_or("#000000");
        assert!(content.contains(&pdf_fill_color(
            template
                .page
                .background_fill
                .as_deref()
                .unwrap_or("#ffffff")
        )));
        assert!(content.contains(&pdf_stroke_color(border_color)));
        assert!(content.contains(&format!(
            "{} w",
            format_pdf_number(mm_to_points(border_width))
        )));
        assert!(content.contains(&format!("{} {} re S", width, height)));

        let expected_texts = vec![
            format!("({})", escape_pdf_text(&format!("job:{}", request.job_id))),
            format!("({})", escape_pdf_text(&format!("brand:{}", request.brand))),
            format!("({})", escape_pdf_text(&format!("sku:{}", request.sku))),
            format!("({})", escape_pdf_text(&format!("jan:{}", request.jan))),
            format!("({})", escape_pdf_text(&format!("qty:{}", request.qty))),
            format!(
                "({})",
                escape_pdf_text(&format!("template:{}", request.template_version))
            ),
        ];
        for text in expected_texts {
            assert!(content.contains(&text));
        }

        let text_positions = parse_text_positions(&content);
        assert_eq!(text_positions.len(), 6);
        for (x, y) in text_positions {
            assert!(x > 0.0 && x <= mm_to_points(template.page.width_mm));
            assert!(y > 0.0 && y <= mm_to_points(template.page.height_mm));
        }
    }

    #[test]
    fn pdf_escapes_control_and_reserved_chars_in_text_fields() {
        let request = RenderLabelRequest {
            jan: "4006381333(91)\nline2\t\\special".to_string(),
            ..sample_request()
        };

        let actual = render_pdf(&request).expect("render_pdf should resolve known template");
        let (content_bytes, _) =
            extract_stream_content(&actual).expect("stream content should be present");
        let content =
            String::from_utf8(content_bytes).expect("pdf stream content should stay ascii");
        let expected_fragment = format!("({})", escape_pdf_text(&format!("jan:{}", request.jan)));
        assert!(
            content.contains(&expected_fragment),
            "control and reserved chars should be escaped in pdf text stream"
        );
        assert!(
            !content.contains("line2\t\\special"),
            "raw tab should not remain in PDF text"
        );
    }

    #[test]
    fn template_files_are_versioned_and_parsable() {
        let template =
            resolve_template("basic-50x30@v1").expect("expected known template version to resolve");

        assert_eq!(template.schema_version, "template-spec-v1");
        assert_eq!(template.template_version, "basic-50x30@v1");
        assert_eq!(template.page.width_mm, 50.0);
        assert_eq!(template.page.height_mm, 30.0);
        assert_eq!(template.fields.len(), 6);
    }

    #[test]
    fn unknown_template_version_is_reported_as_lookup_error() {
        let error = resolve_template("missing-template@v9")
            .expect_err("unknown template versions should now return a bounded error");
        match error {
            TemplateResolutionError::UnknownTemplateVersion {
                requested,
                mut known_versions,
            } => {
                known_versions.sort_unstable();
                let mut expected_known_versions = expected_known_versions();
                expected_known_versions.sort_unstable();

                assert_eq!(requested, "missing-template@v9");
                assert_eq!(known_versions, expected_known_versions);
            }
            _ => panic!("expected unknown-template resolution error"),
        }
    }

    #[test]
    fn template_catalog_exposes_available_versions() {
        let catalog = template_catalog().expect("expected a valid manifest");

        assert_eq!(catalog.schema_version, "template-manifest-v1");
        assert_eq!(catalog.default_template_version, "basic-50x30@v1");
        assert_eq!(catalog.templates.len(), 1);
        assert_eq!(catalog.templates[0].version, "basic-50x30@v1");
        assert!(catalog.templates[0].enabled);
    }

    #[test]
    fn every_enabled_template_in_manifest_is_resolvable() {
        let catalog = template_catalog().expect("expected a valid manifest");
        for entry in catalog.templates.iter().filter(|entry| entry.enabled) {
            assert!(
                !entry.version.trim().is_empty(),
                "manifest entry version should not be empty"
            );
            let template =
                resolve_template(&entry.version).expect("enabled manifest entries must resolve");
            assert!(!template.fields.is_empty());
            assert_eq!(template.template_version, entry.version);
        }
    }

    #[test]
    fn malformed_catalog_is_reported_as_template_error() {
        let malformed = r#"{"schema_version":"template-manifest-v1","default_template_version":"basic-50x30@v1"}"#;
        let error = parse_template_catalog_from_str(malformed, "malformed-test-manifest.json")
            .expect_err("catalog with missing required fields should error");

        match error {
            TemplateResolutionError::MalformedTemplateSpec {
                source, message, ..
            } => {
                assert_eq!(source, "malformed-test-manifest.json");
                assert!(!message.is_empty());
            }
            _ => panic!("expected malformed template error for broken catalog"),
        }
    }

    #[test]
    fn template_catalog_rejects_non_array_templates() {
        let malformed = r#"{
            "schema_version": "template-manifest-v1",
            "default_template_version": "basic-50x30@v1",
            "templates": {}
        }"#;
        let error = parse_template_catalog_from_str(malformed, "malformed-test-manifest.json")
            .expect_err("catalog with non-array templates should error");

        match error {
            TemplateResolutionError::MalformedTemplateSpec {
                source, message, ..
            } => {
                assert_eq!(source, "malformed-test-manifest.json");
                assert!(
                    message.contains("array") || message.contains("sequence"),
                    "{message}"
                );
            }
            _ => panic!("expected malformed template error for non-array templates"),
        }
    }

    #[test]
    fn template_catalog_with_overlay_merges_packaged_and_local_entries() {
        let catalog_root = setup_local_template_overlay_dir("template-overlay-merge");
        let local_manifest = catalog_root.join("template-manifest.json");
        let local_overlay_template = catalog_root.join("local-overlay.json");
        let local_only_template = catalog_root.join("local-only.json");

        write_local_template_file(
            &local_overlay_template,
            r##"
            {
              "schema_version": "template-spec-v1",
              "template_version": "basic-50x30@v1",
              "label_name": "overlay-basic",
              "page": { "width_mm": 50, "height_mm": 30, "background_fill": "#00ff00" },
              "border": { "visible": false },
              "fields": [
                {
                  "name": "sku",
                  "x_mm": 2,
                  "y_mm": 5,
                  "font_size_mm": 3,
                  "template": "sku:{sku}"
                }
              ]
            }
            "##,
        );
        write_local_template_file(
            &local_only_template,
            r##"
            {
              "schema_version": "template-spec-v1",
              "template_version": "local-only@v1",
              "label_name": "local-only",
              "page": { "width_mm": 30, "height_mm": 20, "background_fill": "#0000ff" },
              "fields": [
                {
                  "name": "sku",
                  "x_mm": 2,
                  "y_mm": 5,
                  "font_size_mm": 3,
                  "template": "sku:{sku}"
                }
              ]
            }
            "##,
        );
        fs::write(
            &local_manifest,
            r#"{
  "schema_version": "template-manifest-v1",
  "default_template_version": "local-only@v1",
  "templates": [
    {
      "version": "basic-50x30@v1",
      "path": "local-overlay.json",
      "label_name": "overlay-basic",
      "enabled": true
    },
    {
      "version": "local-only@v1",
      "path": "local-only.json",
      "label_name": "local-only",
      "enabled": true
    }
  ]
}"#,
        )
        .expect("should write local manifest");

        let catalog = template_catalog_with_overlay(Some(&local_manifest))
            .expect("overlay catalog should resolve");
        assert_eq!(catalog.default_template_version, "local-only@v1");
        assert_eq!(catalog.templates.len(), 2);
        assert_eq!(catalog.templates[0].version, "basic-50x30@v1");
        assert_eq!(catalog.templates[1].version, "local-only@v1");

        let overridden = resolve_template_with_overlay("basic-50x30@v1", Some(&local_manifest))
            .expect("overlay version should resolve from local template");
        assert_eq!(
            overridden.page.background_fill.as_deref(),
            Some("#00ff00"),
            "local catalog entry should override packaged path for same version"
        );

        let local_only = resolve_template_with_overlay("local-only@v1", Some(&local_manifest))
            .expect("local-only version should resolve");
        assert_eq!(
            local_only.page.background_fill.as_deref(),
            Some("#0000ff"),
            "local-only version should be available from overlay"
        );
    }

    #[test]
    fn template_catalog_with_overlay_respects_local_disable() {
        let catalog_root = setup_local_template_overlay_dir("template-overlay-disable");
        let local_manifest = catalog_root.join("template-manifest.json");
        let local_template = catalog_root.join("basic-50x30@v1.json");

        write_local_template_file(
            &local_template,
            r##"
            {
              "schema_version": "template-spec-v1",
              "template_version": "basic-50x30@v1",
              "label_name": "disabled-overlay",
              "page": { "width_mm": 50, "height_mm": 30, "background_fill": "#ff0000" },
              "fields": [
                {
                  "name": "sku",
                  "x_mm": 2,
                  "y_mm": 5,
                  "font_size_mm": 3,
                  "template": "sku:{sku}"
                }
              ]
            }
            "##,
        );
        fs::write(
            &local_manifest,
            r#"{
  "schema_version": "template-manifest-v1",
  "default_template_version": "basic-50x30@v1",
  "templates": [
    {
      "version": "basic-50x30@v1",
      "path": "basic-50x30@v1.json",
      "label_name": "disabled-overlay",
      "enabled": false
    }
  ]
}"#,
        )
        .expect("should write local manifest");

        let error = resolve_template_with_overlay("basic-50x30@v1", Some(&local_manifest))
            .expect_err("disabled local overlay should hide template");
        assert!(matches!(
            error,
            TemplateResolutionError::UnknownTemplateVersion { requested, .. } if requested == "basic-50x30@v1"
        ));
    }

    #[test]
    fn render_svg_unknown_template_returns_error() {
        let request = RenderLabelRequest {
            template_version: "missing-template@v9".to_string(),
            ..sample_request()
        };

        let error = render_svg(&request).expect_err("unknown template versions should error");
        match error {
            TemplateResolutionError::UnknownTemplateVersion { requested, .. } => {
                assert_eq!(requested, "missing-template@v9")
            }
            _ => panic!("expected unknown template error from render_svg"),
        }
    }

    #[test]
    fn render_pdf_unknown_template_returns_error() {
        let request = RenderLabelRequest {
            template_version: "missing-template@v9".to_string(),
            ..sample_request()
        };

        let error = render_pdf(&request).expect_err("unknown template versions should error");
        match error {
            TemplateResolutionError::UnknownTemplateVersion {
                requested,
                known_versions,
            } => {
                assert_eq!(requested, "missing-template@v9");
                assert_eq!(known_versions, expected_known_versions());
            }
            _ => panic!("expected unknown template error from render_pdf"),
        }
    }

    fn parse_media_box_values(line: &str) -> Option<Vec<f64>> {
        let after_media_box = line.split("/MediaBox").nth(1)?;
        let remaining = after_media_box.split_once('[')?.1;
        let end = remaining.find(']')?;
        let values = &remaining[..end];
        let mut parsed = Vec::with_capacity(4);
        for token in values.split_whitespace() {
            parsed.push(token.parse::<f64>().ok()?);
        }
        Some(parsed)
    }

    fn extract_stream_content(pdf: &[u8]) -> Option<(Vec<u8>, usize)> {
        let stream_start = find_subslice(pdf, b"stream\n")?;
        let stream_data_start = stream_start + b"stream\n".len();
        let stream_end =
            stream_data_start + find_subslice(&pdf[stream_data_start..], b"endstream")?;
        let content = pdf[stream_data_start..stream_end].to_vec();
        Some((content, stream_data_start))
    }

    fn extract_content_length(pdf: &str) -> Option<usize> {
        pdf.lines()
            .find(|line| line.starts_with("<< /Length "))
            .and_then(|line| line.split_whitespace().nth(2))
            .and_then(|value| value.parse().ok())
    }

    fn find_subslice(haystack: &[u8], needle: &[u8]) -> Option<usize> {
        haystack
            .windows(needle.len())
            .position(|window| window == needle)
    }

    fn parse_text_positions(stream: &str) -> Vec<(f64, f64)> {
        stream
            .lines()
            .filter_map(|line| {
                let mut parts = line.split_whitespace();
                let x = parts.next()?;
                let y = parts.next()?;
                let op = parts.next()?;
                if op == "Td" {
                    let x = x.parse().ok()?;
                    let y = y.parse().ok()?;
                    Some((x, y))
                } else {
                    None
                }
            })
            .collect()
    }

    fn setup_local_template_overlay_dir(name: &str) -> PathBuf {
        let nanos = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .expect("system time should be available")
            .as_nanos();
        let dir = std::env::temp_dir().join(format!("{name}-{nanos}"));
        let _ = fs::remove_dir_all(&dir);
        fs::create_dir_all(&dir).expect("local overlay directory should be created");
        dir
    }

    fn write_local_template_file(path: &PathBuf, source: &str) {
        fs::write(path, source).expect("template file should be written");
    }

    fn approx_eq(a: f64, b: f64, epsilon: f64) -> bool {
        (a - b).abs() <= epsilon
    }

    fn expected_known_versions() -> Vec<String> {
        vec!["basic-50x30@v1".to_string()]
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
