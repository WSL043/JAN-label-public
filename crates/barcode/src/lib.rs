use std::process::Command;

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum BarcodeFormat {
    Jan13,
}

impl BarcodeFormat {
    fn zint_symbology(&self) -> &'static str {
        match self {
            Self::Jan13 => "EANX_CHK",
        }
    }
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

impl BarcodeError {
    fn spawn_failed(binary_path: &str, error: &std::io::Error) -> Self {
        Self {
            message: format!("failed to start zint CLI '{binary_path}': {error}"),
        }
    }

    fn process_failed(binary_path: &str, exit_code: Option<i32>, stderr: &[u8]) -> Self {
        let stderr = String::from_utf8_lossy(stderr).trim().to_string();
        let status = exit_code
            .map(|code| code.to_string())
            .unwrap_or_else(|| "terminated by signal".to_string());
        let message = if stderr.is_empty() {
            format!("zint CLI '{binary_path}' failed with exit code {status}")
        } else {
            format!("zint CLI '{binary_path}' failed with exit code {status}: {stderr}")
        };

        Self { message }
    }
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
            "--barcode".to_string(),
            request.format.zint_symbology().to_string(),
            "--data".to_string(),
            request.jan.clone(),
            "--output".to_string(),
            request.output_path.clone(),
        ]
    }
}

impl BarcodeEngine for ZintCli {
    fn render(&self, request: &BarcodeRequest) -> Result<BarcodeArtifact, BarcodeError> {
        let args = self.command_line(request);
        let output = Command::new(&args[0])
            .args(&args[1..])
            .output()
            .map_err(|error| BarcodeError::spawn_failed(&self.binary_path, &error))?;

        if !output.status.success() {
            return Err(BarcodeError::process_failed(
                &self.binary_path,
                output.status.code(),
                &output.stderr,
            ));
        }

        Ok(BarcodeArtifact {
            output_path: request.output_path.clone(),
        })
    }
}

#[cfg(test)]
mod tests {
    use super::{BarcodeEngine, BarcodeError, BarcodeFormat, BarcodeRequest, ZintCli};
    use std::env;
    use std::fs;
    #[cfg(unix)]
    use std::os::unix::fs::PermissionsExt;
    use std::path::{Path, PathBuf};
    use std::process;
    use std::sync::{Mutex, MutexGuard, OnceLock};
    use std::time::{SystemTime, UNIX_EPOCH};

    const FAKE_ZINT_ARGS_FILE: &str = "FAKE_ZINT_ARGS_FILE";
    const FAKE_ZINT_STDERR: &str = "FAKE_ZINT_STDERR";
    const FAKE_ZINT_EXIT_CODE: &str = "FAKE_ZINT_EXIT_CODE";
    const FAKE_ZINT_OUTPUT_FILE: &str = "FAKE_ZINT_OUTPUT_FILE";

    static ENV_LOCK: OnceLock<Mutex<()>> = OnceLock::new();

    #[test]
    fn command_line_uses_eanx_chk_for_normalized_jan13() {
        let cli = ZintCli {
            binary_path: "zint".to_string(),
        };
        let request = BarcodeRequest {
            jan: "4006381333931".to_string(),
            format: BarcodeFormat::Jan13,
            output_path: "out.svg".to_string(),
        };

        assert_eq!(
            cli.command_line(&request),
            vec![
                "zint".to_string(),
                "--barcode".to_string(),
                "EANX_CHK".to_string(),
                "--data".to_string(),
                "4006381333931".to_string(),
                "--output".to_string(),
                "out.svg".to_string(),
            ]
        );
    }

    #[test]
    fn render_uses_injected_binary_path_and_passes_expected_arguments() {
        let _guard = lock_env();
        let temp_dir = TestDir::new();
        let fake_binary = create_fake_zint(&temp_dir);
        let args_file = temp_dir.path().join("args.txt");
        let output_path = temp_dir.path().join("barcode.svg");
        let _env = EnvGuard::set(&[
            (FAKE_ZINT_ARGS_FILE, Some(args_file.as_os_str())),
            (FAKE_ZINT_OUTPUT_FILE, Some(output_path.as_os_str())),
            (FAKE_ZINT_STDERR, None),
            (FAKE_ZINT_EXIT_CODE, None),
        ]);

        let cli = ZintCli {
            binary_path: fake_binary.to_string_lossy().into_owned(),
        };
        let request = BarcodeRequest {
            jan: "4006381333931".to_string(),
            format: BarcodeFormat::Jan13,
            output_path: output_path.to_string_lossy().into_owned(),
        };

        let artifact = cli.render(&request).expect("fake zint should succeed");

        assert_eq!(artifact.output_path, request.output_path);
        assert!(
            output_path.exists(),
            "fake zint should create the output file"
        );

        let actual_args = read_lines(&args_file);
        assert_eq!(
            actual_args,
            vec![
                "--barcode".to_string(),
                "EANX_CHK".to_string(),
                "--data".to_string(),
                "4006381333931".to_string(),
                "--output".to_string(),
                request.output_path,
            ]
        );
    }

    #[test]
    fn render_returns_exit_code_and_stderr_from_failed_process() {
        let _guard = lock_env();
        let temp_dir = TestDir::new();
        let fake_binary = create_fake_zint(&temp_dir);
        let stderr_message = "zint reported invalid option";
        let _env = EnvGuard::set(&[
            (FAKE_ZINT_ARGS_FILE, None),
            (FAKE_ZINT_OUTPUT_FILE, None),
            (FAKE_ZINT_STDERR, Some(stderr_message.as_ref())),
            (FAKE_ZINT_EXIT_CODE, Some("17".as_ref())),
        ]);

        let cli = ZintCli {
            binary_path: fake_binary.to_string_lossy().into_owned(),
        };
        let request = BarcodeRequest {
            jan: "4006381333931".to_string(),
            format: BarcodeFormat::Jan13,
            output_path: temp_dir
                .path()
                .join("barcode.svg")
                .to_string_lossy()
                .into_owned(),
        };

        let err = cli.render(&request).expect_err("fake zint should fail");
        assert_eq!(
            err,
            BarcodeError {
                message: format!(
                    "zint CLI '{}' failed with exit code 17: {stderr_message}",
                    cli.binary_path
                ),
            }
        );
    }

    fn lock_env() -> MutexGuard<'static, ()> {
        ENV_LOCK
            .get_or_init(|| Mutex::new(()))
            .lock()
            .expect("test env lock must be available")
    }

    fn create_fake_zint(temp_dir: &TestDir) -> PathBuf {
        #[cfg(windows)]
        let path = temp_dir.path().join("fake-zint.cmd");
        #[cfg(not(windows))]
        let path = temp_dir.path().join("fake-zint.sh");

        #[cfg(windows)]
        let script = r#"@echo off
setlocal
if defined FAKE_ZINT_ARGS_FILE break > "%FAKE_ZINT_ARGS_FILE%"
:args_loop
if "%~1"=="" goto after_args
if defined FAKE_ZINT_ARGS_FILE >> "%FAKE_ZINT_ARGS_FILE%" echo(%~1
shift
goto args_loop
:after_args
if defined FAKE_ZINT_STDERR >&2 echo(%FAKE_ZINT_STDERR%
if defined FAKE_ZINT_OUTPUT_FILE type nul > "%FAKE_ZINT_OUTPUT_FILE%"
if not defined FAKE_ZINT_EXIT_CODE exit /b 0
exit /b %FAKE_ZINT_EXIT_CODE%
"#;
        #[cfg(not(windows))]
        let script = r#"#!/bin/sh
if [ -n "${FAKE_ZINT_ARGS_FILE:-}" ]; then
  : > "$FAKE_ZINT_ARGS_FILE"
  for arg in "$@"; do
    printf '%s\n' "$arg" >> "$FAKE_ZINT_ARGS_FILE"
  done
fi
if [ -n "${FAKE_ZINT_STDERR:-}" ]; then
  printf '%s\n' "$FAKE_ZINT_STDERR" >&2
fi
if [ -n "${FAKE_ZINT_OUTPUT_FILE:-}" ]; then
  : > "$FAKE_ZINT_OUTPUT_FILE"
fi
exit "${FAKE_ZINT_EXIT_CODE:-0}"
"#;

        fs::write(&path, script).expect("fake zint script must be written");
        #[cfg(unix)]
        {
            let mut permissions = fs::metadata(&path)
                .expect("fake zint metadata must exist")
                .permissions();
            permissions.set_mode(0o755);
            fs::set_permissions(&path, permissions).expect("fake zint must be executable");
        }

        path
    }

    fn read_lines(path: &Path) -> Vec<String> {
        fs::read_to_string(path)
            .expect("captured args must exist")
            .lines()
            .map(|line| line.to_string())
            .collect()
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
            let path =
                env::temp_dir().join(format!("barcode-zint-test-{}-{unique}", process::id()));
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

    struct EnvGuard {
        saved: Vec<(&'static str, Option<std::ffi::OsString>)>,
    }

    impl EnvGuard {
        fn set(vars: &[(&'static str, Option<&std::ffi::OsStr>)]) -> Self {
            let mut saved = Vec::with_capacity(vars.len());
            for (key, value) in vars {
                saved.push((*key, env::var_os(key)));
                match value {
                    Some(value) => env::set_var(key, value),
                    None => env::remove_var(key),
                }
            }
            Self { saved }
        }
    }

    impl Drop for EnvGuard {
        fn drop(&mut self) {
            for (key, value) in self.saved.drain(..).rev() {
                match value {
                    Some(value) => env::set_var(key, value),
                    None => env::remove_var(key),
                }
            }
        }
    }
}
