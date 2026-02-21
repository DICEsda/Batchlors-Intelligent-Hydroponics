import { TestBed } from '@angular/core/testing';
import { provideExperimentalZonelessChangeDetection } from '@angular/core';

import { NotificationService, Notification } from './notification.service';

describe('NotificationService', () => {
  let service: NotificationService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideExperimentalZonelessChangeDetection()],
    });
    service = TestBed.inject(NotificationService);
  });

  // ---------------------------------------------------------------------------
  // Initial state
  // ---------------------------------------------------------------------------

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should have empty allNotifications initially', () => {
    expect(service.allNotifications()).toEqual([]);
  });

  it('should have unreadCount of 0 initially', () => {
    expect(service.unreadCount()).toBe(0);
  });

  // ---------------------------------------------------------------------------
  // addNotification
  // ---------------------------------------------------------------------------

  it('should add a notification to the front of the list', () => {
    service.addNotification('info', 'Title1', 'Msg1');
    service.addNotification('success', 'Title2', 'Msg2');

    const all = service.allNotifications();
    expect(all.length).toBe(2);
    // newest first
    expect(all[0].title).toBe('Title2');
    expect(all[1].title).toBe('Title1');
  });

  it('should generate a unique ID for each notification', () => {
    service.addNotification('info', 'T1', 'M1');
    service.addNotification('info', 'T2', 'M2');

    const ids = service.allNotifications().map(n => n.id);
    expect(ids[0]).not.toBe(ids[1]);
  });

  it('should set read to false on new notifications', () => {
    service.addNotification('warning', 'Title', 'Msg');

    expect(service.allNotifications()[0].read).toBe(false);
  });

  it('should set a timestamp on new notifications', () => {
    const before = new Date();
    service.addNotification('error', 'Title', 'Msg');
    const after = new Date();

    const ts = service.allNotifications()[0].timestamp;
    expect(ts.getTime()).toBeGreaterThanOrEqual(before.getTime());
    expect(ts.getTime()).toBeLessThanOrEqual(after.getTime());
  });

  it('should cap notifications at 50 (discard oldest)', () => {
    for (let i = 0; i < 55; i++) {
      service.addNotification('info', `T${i}`, `M${i}`);
    }

    expect(service.allNotifications().length).toBe(50);
    // The most recent should be at index 0
    expect(service.allNotifications()[0].title).toBe('T54');
  });

  it('should preserve actionLabel and actionCallback', () => {
    const callback = jasmine.createSpy('action');
    service.addNotification('info', 'T', 'M', 'Click me', callback);

    const notif = service.allNotifications()[0];
    expect(notif.actionLabel).toBe('Click me');
    expect(notif.actionCallback).toBe(callback);
  });

  // ---------------------------------------------------------------------------
  // markAsRead
  // ---------------------------------------------------------------------------

  it('should mark a specific notification as read', () => {
    service.addNotification('info', 'T1', 'M1');
    service.addNotification('info', 'T2', 'M2');

    const id = service.allNotifications()[1].id; // older one
    service.markAsRead(id);

    expect(service.allNotifications()[1].read).toBe(true);
    expect(service.allNotifications()[0].read).toBe(false); // newest unaffected
  });

  it('should not error when markAsRead is called with non-existent id', () => {
    service.addNotification('info', 'T', 'M');

    expect(() => service.markAsRead('non-existent-id')).not.toThrow();
    // Original notification unchanged
    expect(service.allNotifications()[0].read).toBe(false);
  });

  // ---------------------------------------------------------------------------
  // markAllAsRead
  // ---------------------------------------------------------------------------

  it('should mark all notifications as read', () => {
    service.addNotification('info', 'T1', 'M1');
    service.addNotification('warning', 'T2', 'M2');
    service.addNotification('error', 'T3', 'M3');

    service.markAllAsRead();

    const allRead = service.allNotifications().every(n => n.read);
    expect(allRead).toBe(true);
  });

  // ---------------------------------------------------------------------------
  // deleteNotification
  // ---------------------------------------------------------------------------

  it('should remove a specific notification by id', () => {
    service.addNotification('info', 'T1', 'M1');
    service.addNotification('info', 'T2', 'M2');

    const idToDelete = service.allNotifications()[0].id;
    service.deleteNotification(idToDelete);

    expect(service.allNotifications().length).toBe(1);
    expect(service.allNotifications()[0].title).toBe('T1');
  });

  // ---------------------------------------------------------------------------
  // clearAll
  // ---------------------------------------------------------------------------

  it('should clear all notifications', () => {
    service.addNotification('info', 'T1', 'M1');
    service.addNotification('info', 'T2', 'M2');

    service.clearAll();

    expect(service.allNotifications()).toEqual([]);
    expect(service.unreadCount()).toBe(0);
  });

  // ---------------------------------------------------------------------------
  // unreadCount
  // ---------------------------------------------------------------------------

  it('should correctly count unread notifications', () => {
    service.addNotification('info', 'T1', 'M1');
    service.addNotification('info', 'T2', 'M2');
    service.addNotification('info', 'T3', 'M3');

    expect(service.unreadCount()).toBe(3);

    const id = service.allNotifications()[0].id;
    service.markAsRead(id);

    expect(service.unreadCount()).toBe(2);
  });

  it('should return 0 unreadCount after markAllAsRead', () => {
    service.addNotification('info', 'T1', 'M1');
    service.addNotification('info', 'T2', 'M2');

    service.markAllAsRead();

    expect(service.unreadCount()).toBe(0);
  });

  // ---------------------------------------------------------------------------
  // Convenience methods – each sets the correct type
  // ---------------------------------------------------------------------------

  it('info() should add a notification with type "info"', () => {
    service.info('Info Title', 'Info Msg');
    expect(service.allNotifications()[0].type).toBe('info');
    expect(service.allNotifications()[0].title).toBe('Info Title');
  });

  it('success() should add a notification with type "success"', () => {
    service.success('S Title', 'S Msg');
    expect(service.allNotifications()[0].type).toBe('success');
  });

  it('warning() should add a notification with type "warning"', () => {
    service.warning('W Title', 'W Msg');
    expect(service.allNotifications()[0].type).toBe('warning');
  });

  it('error() should add a notification with type "error"', () => {
    service.error('E Title', 'E Msg');
    expect(service.allNotifications()[0].type).toBe('error');
  });

  it('discovery() should add a notification with type "discovery"', () => {
    service.discovery('D Title', 'D Msg');
    expect(service.allNotifications()[0].type).toBe('discovery');
  });

  // ---------------------------------------------------------------------------
  // Ordering
  // ---------------------------------------------------------------------------

  it('should maintain newest-first order across multiple adds', () => {
    service.addNotification('info', 'First', 'M');
    service.addNotification('info', 'Second', 'M');
    service.addNotification('info', 'Third', 'M');

    const titles = service.allNotifications().map(n => n.title);
    expect(titles).toEqual(['Third', 'Second', 'First']);
  });
});
