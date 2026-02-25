import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  lucideArrowLeft,
  lucideEgg,
  lucideThermometer,
  lucideDroplets,
  lucideSprout,
  lucideSun,
  lucideCalendarClock,
  lucideBell
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
  selector: 'app-incubators',
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
      lucideEgg,
      lucideThermometer,
      lucideDroplets,
      lucideSprout,
      lucideSun,
      lucideCalendarClock,
      lucideBell
    })
  ],
  templateUrl: './incubators.component.html',
  styleUrl: './incubators.component.scss',
})
export class IncubatorsComponent {}
