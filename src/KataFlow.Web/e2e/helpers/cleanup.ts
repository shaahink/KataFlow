import { deleteAllSessions } from './api';

export async function cleanupSessions() {
  try {
    await deleteAllSessions();
  } catch {
    // Ignore cleanup errors
  }
}
