import { DialogConfig } from '@angular/cdk/dialog';

/** Same look for every dialog (classes live in styles.css). Spread it and add `data`. */
export const DIALOG_DEFAULTS = {
  panelClass: 'app-dialog-panel',
  backdropClass: 'app-dialog-backdrop',
  width: '480px',
  maxWidth: 'calc(100vw - 32px)',
  autoFocus: 'first-tabbable', // honors [cdkFocusInitial] inside the dialog
} as const satisfies Partial<DialogConfig>;
