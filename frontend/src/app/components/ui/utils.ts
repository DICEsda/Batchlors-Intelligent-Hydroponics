import { clsx, type ClassValue } from 'clsx';
import { twMerge } from 'tailwind-merge';

/**
 * Utility function to merge Tailwind CSS classes
 * Combines clsx for conditional classes with tailwind-merge for deduplication
 */
export function hlm(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}

/**
 * Type helper for component variants
 */
export type ClassArray = ClassValue[];
