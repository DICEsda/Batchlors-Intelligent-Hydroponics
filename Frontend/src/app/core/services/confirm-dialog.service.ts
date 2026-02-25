import { Injectable, signal } from '@angular/core';

export interface ConfirmDialogOptions {
  title: string;
  message: string;
  confirmText?: string;
  cancelText?: string;
  type?: 'danger' | 'warning' | 'info';
}

interface ConfirmDialogState extends ConfirmDialogOptions {
  isOpen: boolean;
  onConfirm?: () => void;
  onCancel?: () => void;
}

@Injectable({
  providedIn: 'root'
})
export class ConfirmDialogService {
  readonly state = signal<ConfirmDialogState>({
    isOpen: false,
    title: '',
    message: '',
    confirmText: 'Confirm',
    cancelText: 'Cancel',
    type: 'info'
  });

  /**
   * Show confirmation dialog
   * @returns Promise that resolves to true if confirmed, false if cancelled
   */
  confirm(options: ConfirmDialogOptions): Promise<boolean> {
    return new Promise((resolve) => {
      this.state.set({
        isOpen: true,
        title: options.title,
        message: options.message,
        confirmText: options.confirmText || 'Confirm',
        cancelText: options.cancelText || 'Cancel',
        type: options.type || 'info',
        onConfirm: () => {
          this.close();
          resolve(true);
        },
        onCancel: () => {
          this.close();
          resolve(false);
        }
      });
    });
  }

  /**
   * Quick method for dangerous/destructive actions
   */
  confirmDanger(title: string, message: string, confirmText: string = 'Delete'): Promise<boolean> {
    return this.confirm({
      title,
      message,
      confirmText,
      cancelText: 'Cancel',
      type: 'danger'
    });
  }

  /**
   * Quick method for warnings
   */
  confirmWarning(title: string, message: string, confirmText: string = 'Continue'): Promise<boolean> {
    return this.confirm({
      title,
      message,
      confirmText,
      cancelText: 'Cancel',
      type: 'warning'
    });
  }

  close() {
    this.state.update(s => ({ ...s, isOpen: false }));
  }
}
