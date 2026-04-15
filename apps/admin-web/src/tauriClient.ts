import type { DispatchRequest, PrintDispatchResult } from "@label/job-schema";
import { invoke, isTauri } from "@tauri-apps/api/core";

const DISPATCH_PRINT_JOB_COMMAND = "dispatch_print_job";
const PRINT_BRIDGE_STATUS_COMMAND = "print_bridge_status";

export type PrintBridgeStatus = {
  availableAdapters: string[];
  resolvedZintPath: string;
  proofOutputDir: string;
  printOutputDir: string;
  spoolOutputDir: string;
  printAdapterKind: string;
  windowsPrinterName: string;
  allowWithoutProofEnabled: boolean;
  warnings: string[];
};

export function isTauriConnected(): boolean {
  return isTauri();
}

export async function dispatchPrintJob(request: DispatchRequest): Promise<PrintDispatchResult> {
  if (!isTauriConnected()) {
    throw new Error(
      "Browser preview mode: desktop bridge unavailable. Connect to desktop shell to submit jobs.",
    );
  }
  return invoke<PrintDispatchResult>(DISPATCH_PRINT_JOB_COMMAND, { request });
}

export async function fetchPrintBridgeStatus(): Promise<PrintBridgeStatus> {
  if (!isTauriConnected()) {
    throw new Error(
      "Browser preview mode: desktop bridge unavailable. Connect to desktop shell to view status.",
    );
  }
  return invoke<PrintBridgeStatus>(PRINT_BRIDGE_STATUS_COMMAND, {});
}
