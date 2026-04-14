export type JanInput = {
  raw: string;
  normalized: string;
  source: "manual" | "import";
};

export type PrinterProfile = {
  id: string;
  adapter: "pdf" | "windows-spooler" | "zpl" | "tspl" | "qz";
  paperSize: string;
  dpi: number;
  scalePolicy: "fixed-100";
};

export type LabelTemplateRef = {
  id: string;
  version: string;
};

export type PrintJobDraft = {
  jobId: string;
  parentSku: string;
  sku: string;
  jan: JanInput;
  qty: number;
  brand: string;
  template: LabelTemplateRef;
  printerProfile: PrinterProfile;
  actor: string;
  requestedAt: string;
};

