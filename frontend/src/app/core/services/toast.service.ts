import { Injectable, ApplicationRef, createComponent, EnvironmentInjector, inject, signal } from '@angular/core';
import { ToastContainerComponent, Toast, ToastType, ToastAction, ToastSecondaryAction } from '../../components/ui/toast';

@Injectable({
  providedIn: 'root'
})
export class ToastService {
  private containerRef: ToastContainerComponent | null = null;
  private readonly appRef = inject(ApplicationRef);
  private readonly injector = inject(EnvironmentInjector);

  private ensureContainer(): ToastContainerComponent {
    if (!this.containerRef) {
      // Create the toast container component dynamically
      const componentRef = createComponent(ToastContainerComponent, {
        environmentInjector: this.injector
      });
      
      // Append to body
      document.body.appendChild(componentRef.location.nativeElement);
      
      // Attach to change detection
      this.appRef.attachView(componentRef.hostView);
      
      this.containerRef = componentRef.instance;
    }
    return this.containerRef;
  }

  /**
   * Show a toast notification
   */
  show(options: Omit<Toast, 'id'>): string {
    const container = this.ensureContainer();
    return container.addToast(options);
  }

  /**
   * Show a success toast
   */
  success(title: string, message?: string, duration?: number): string {
    return this.show({ type: 'success', title, message, duration });
  }

  /**
   * Show an error toast
   */
  error(title: string, message?: string, duration?: number): string {
    return this.show({ type: 'error', title, message, duration: duration ?? 8000 });
  }

  /**
   * Show a warning toast
   */
  warning(title: string, message?: string, duration?: number): string {
    return this.show({ type: 'warning', title, message, duration });
  }

  /**
   * Show an info toast
   */
  info(title: string, message?: string, duration?: number): string {
    return this.show({ type: 'info', title, message, duration });
  }

  /**
   * Show a discovery toast (for new tower/device discovered)
   */
  discovery(title: string, message?: string, duration?: number): string {
    return this.show({ type: 'discovery', title, message, duration: duration ?? 7000 });
  }

  /**
   * Show an urgent action toast with an action button and optional secondary action
   */
  urgent(
    title: string, 
    message: string, 
    actionLabel: string, 
    actionCallback: () => void, 
    duration?: number,
    secondaryLabel?: string,
    secondaryCallback?: () => void
  ): string {
    return this.show({
      type: 'urgent',
      title,
      message,
      duration: duration ?? 0, // Don't auto-dismiss urgent toasts by default
      dismissible: true,
      action: {
        label: actionLabel,
        callback: actionCallback
      },
      secondaryAction: secondaryLabel && secondaryCallback ? {
        label: secondaryLabel,
        callback: secondaryCallback
      } : undefined
    });
  }

  /**
   * Dismiss a specific toast by ID
   */
  dismiss(id: string): void {
    this.containerRef?.dismiss(id);
  }

  /**
   * Clear all toasts
   */
  clear(): void {
    this.containerRef?.clear();
  }
}
