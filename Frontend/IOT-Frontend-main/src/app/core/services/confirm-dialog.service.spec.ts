import { TestBed } from '@angular/core/testing';
import { provideExperimentalZonelessChangeDetection } from '@angular/core';

import { ConfirmDialogService } from './confirm-dialog.service';

describe('ConfirmDialogService', () => {
  let service: ConfirmDialogService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideExperimentalZonelessChangeDetection()],
    });
    service = TestBed.inject(ConfirmDialogService);
  });

  // ---------------------------------------------------------------------------
  // Initial state
  // ---------------------------------------------------------------------------

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should have isOpen false initially', () => {
    expect(service.state().isOpen).toBe(false);
  });

  it('should have empty title and message initially', () => {
    expect(service.state().title).toBe('');
    expect(service.state().message).toBe('');
  });

  // ---------------------------------------------------------------------------
  // confirm()
  // ---------------------------------------------------------------------------

  it('should set isOpen to true when confirm() is called', () => {
    service.confirm({ title: 'Test', message: 'Are you sure?' });

    expect(service.state().isOpen).toBe(true);
  });

  it('should set title and message from options', () => {
    service.confirm({ title: 'Delete Item', message: 'This cannot be undone.' });

    expect(service.state().title).toBe('Delete Item');
    expect(service.state().message).toBe('This cannot be undone.');
  });

  it('should resolve to true when onConfirm is called', async () => {
    const promise = service.confirm({ title: 'T', message: 'M' });

    // Simulate user clicking confirm
    service.state().onConfirm!();

    const result = await promise;
    expect(result).toBe(true);
  });

  it('should resolve to false when onCancel is called', async () => {
    const promise = service.confirm({ title: 'T', message: 'M' });

    // Simulate user clicking cancel
    service.state().onCancel!();

    const result = await promise;
    expect(result).toBe(false);
  });

  it('should use default confirmText, cancelText, and type when not provided', () => {
    service.confirm({ title: 'T', message: 'M' });

    expect(service.state().confirmText).toBe('Confirm');
    expect(service.state().cancelText).toBe('Cancel');
    expect(service.state().type).toBe('info');
  });

  it('should use custom confirmText, cancelText, and type when provided', () => {
    service.confirm({
      title: 'T',
      message: 'M',
      confirmText: 'Yes, do it',
      cancelText: 'No, go back',
      type: 'danger',
    });

    expect(service.state().confirmText).toBe('Yes, do it');
    expect(service.state().cancelText).toBe('No, go back');
    expect(service.state().type).toBe('danger');
  });

  // ---------------------------------------------------------------------------
  // confirmDanger()
  // ---------------------------------------------------------------------------

  it('should set type to "danger" and default confirmText to "Delete"', () => {
    service.confirmDanger('Delete?', 'Gone forever.');

    expect(service.state().type).toBe('danger');
    expect(service.state().confirmText).toBe('Delete');
    expect(service.state().title).toBe('Delete?');
    expect(service.state().message).toBe('Gone forever.');
  });

  it('should accept a custom confirmText for confirmDanger', () => {
    service.confirmDanger('Remove?', 'Msg', 'Remove Now');

    expect(service.state().confirmText).toBe('Remove Now');
    expect(service.state().type).toBe('danger');
  });

  it('confirmDanger should resolve true on confirm', async () => {
    const promise = service.confirmDanger('T', 'M');
    service.state().onConfirm!();
    expect(await promise).toBe(true);
  });

  // ---------------------------------------------------------------------------
  // confirmWarning()
  // ---------------------------------------------------------------------------

  it('should set type to "warning" and default confirmText to "Continue"', () => {
    service.confirmWarning('Caution', 'Proceed with care.');

    expect(service.state().type).toBe('warning');
    expect(service.state().confirmText).toBe('Continue');
    expect(service.state().title).toBe('Caution');
    expect(service.state().message).toBe('Proceed with care.');
  });

  it('should accept a custom confirmText for confirmWarning', () => {
    service.confirmWarning('Warn', 'Msg', 'Go Ahead');

    expect(service.state().confirmText).toBe('Go Ahead');
    expect(service.state().type).toBe('warning');
  });

  it('confirmWarning should resolve false on cancel', async () => {
    const promise = service.confirmWarning('T', 'M');
    service.state().onCancel!();
    expect(await promise).toBe(false);
  });

  // ---------------------------------------------------------------------------
  // close()
  // ---------------------------------------------------------------------------

  it('should set isOpen to false when close() is called', () => {
    service.confirm({ title: 'T', message: 'M' });
    expect(service.state().isOpen).toBe(true);

    service.close();
    expect(service.state().isOpen).toBe(false);
  });

  // ---------------------------------------------------------------------------
  // Multiple confirms
  // ---------------------------------------------------------------------------

  it('should replace first dialog state when confirm is called again', () => {
    service.confirm({ title: 'First', message: 'M1' });
    service.confirm({ title: 'Second', message: 'M2', type: 'warning' });

    expect(service.state().title).toBe('Second');
    expect(service.state().message).toBe('M2');
    expect(service.state().type).toBe('warning');
    expect(service.state().isOpen).toBe(true);
  });

  it('onConfirm should close the dialog automatically', async () => {
    const promise = service.confirm({ title: 'T', message: 'M' });
    service.state().onConfirm!();
    await promise;

    expect(service.state().isOpen).toBe(false);
  });

  it('onCancel should close the dialog automatically', async () => {
    const promise = service.confirm({ title: 'T', message: 'M' });
    service.state().onCancel!();
    await promise;

    expect(service.state().isOpen).toBe(false);
  });
});
