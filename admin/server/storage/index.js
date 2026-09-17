import { FileStorage } from './fileStorage.js';
import { GitHubStorage } from './githubStorage.js';

/** Escolhe o adaptador de armazenamento a partir da configuracao. */
export function createStorage(config) {
  if (config.storage === 'github') return new GitHubStorage(config.github);
  return new FileStorage(config.file);
}
