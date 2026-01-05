import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  lucideArrowLeft,
  lucideBrain,
  lucideLineChart,
  lucideActivity,
  lucideDroplets,
  lucideSun,
  lucideFlaskConical,
  lucideDatabase
} from '@ng-icons/lucide';
import {
  HlmCardDirective,
  HlmCardHeaderDirective,
  HlmCardTitleDirective,
  HlmCardDescriptionDirective,
  HlmCardContentDirective,
} from '../../components/ui/card';
import { HlmBadgeDirective } from '../../components/ui/badge';
import { HlmButtonDirective } from '../../components/ui/button';
import { HlmIconDirective } from '../../components/ui/icon';

@Component({
  selector: 'app-machine-learning',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    NgIcon,
    HlmCardDirective,
    HlmCardHeaderDirective,
    HlmCardTitleDirective,
    HlmCardDescriptionDirective,
    HlmCardContentDirective,
    HlmBadgeDirective,
    HlmButtonDirective,
    HlmIconDirective,
  ],
  providers: [
    provideIcons({
      lucideArrowLeft,
      lucideBrain,
      lucideLineChart,
      lucideActivity,
      lucideDroplets,
      lucideSun,
      lucideFlaskConical,
      lucideDatabase
    })
  ],
  templateUrl: './machine-learning.component.html',
  styleUrl: './machine-learning.component.scss',
})
export class MachineLearningComponent {}
