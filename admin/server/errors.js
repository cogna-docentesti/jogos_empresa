export class HttpError extends Error {
  constructor(status, message, details) {
    super(message);
    this.status = status;
    this.details = details;
  }
}

export const notFound = (message) => new HttpError(404, message);
export const conflict = (message) => new HttpError(409, message);
export const unprocessable = (message, issues) => new HttpError(422, message, { issues });

/** Lancado pelo storage quando outra gravacao aconteceu no meio do caminho. */
export class StorageConflictError extends Error {}
