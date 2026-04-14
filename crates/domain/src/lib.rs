#[derive(Debug, Clone, PartialEq, Eq)]
pub struct CatalogItem {
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
pub struct Jan {
    digits: String,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum JanError {
    Empty,
    NonDigit,
    InvalidLength { actual: usize },
    InvalidChecksum { expected: char, actual: char },
}

impl Jan {
    pub fn parse(input: &str) -> Result<Self, JanError> {
        let trimmed = input.trim();
        if trimmed.is_empty() {
            return Err(JanError::Empty);
        }
        if !trimmed.chars().all(|ch| ch.is_ascii_digit()) {
            return Err(JanError::NonDigit);
        }

        match trimmed.len() {
            12 => Ok(Self {
                digits: format!("{trimmed}{}", checksum(trimmed)),
            }),
            13 => {
                let expected = checksum(&trimmed[..12]);
                let actual = trimmed.chars().last().unwrap_or_default();
                if expected != actual {
                    return Err(JanError::InvalidChecksum { expected, actual });
                }
                Ok(Self {
                    digits: trimmed.to_string(),
                })
            }
            actual => Err(JanError::InvalidLength { actual }),
        }
    }

    pub fn as_str(&self) -> &str {
        &self.digits
    }
}

fn checksum(body: &str) -> char {
    let total = body
        .chars()
        .rev()
        .enumerate()
        .map(|(index, ch)| ch.to_digit(10).unwrap_or(0) * if index % 2 == 0 { 3 } else { 1 })
        .sum::<u32>();
    let digit = (10 - (total % 10)) % 10;
    char::from_digit(digit, 10).unwrap_or('0')
}

#[cfg(test)]
mod tests {
    use super::{Jan, JanError};

    #[test]
    fn completes_checksum_from_12_digits() {
        let jan = Jan::parse("400638133393").expect("12-digit JAN should normalize");
        assert_eq!(jan.as_str(), "4006381333931");
    }

    #[test]
    fn validates_13_digits() {
        let jan = Jan::parse("4006381333931").expect("13-digit JAN should validate");
        assert_eq!(jan.as_str(), "4006381333931");
    }

    #[test]
    fn rejects_invalid_checksum() {
        let err = Jan::parse("4006381333930").expect_err("checksum mismatch must fail");
        assert_eq!(
            err,
            JanError::InvalidChecksum {
                expected: '1',
                actual: '0'
            }
        );
    }
}

