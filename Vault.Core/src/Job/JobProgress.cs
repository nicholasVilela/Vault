namespace Vault.Core.Job;

public readonly record struct JobProgress(
  int Processed,
  int Skipped,
  int FileCount,
  long CompletedWork,
  long TotalWork
);
