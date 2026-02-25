import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  lucideArrowLeft,
  lucideBuilding,
  lucideThermometer,
  lucideCloud,
  lucideSun,
  lucideDroplets,
  lucideWind,
  lucideLayoutGrid
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
  selector: 'app-greenhouses',
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
      lucideBuilding,
      lucideThermometer,
      lucideCloud,
      lucideSun,
      lucideDroplets,
      lucideWind,
      lucideLayoutGrid
    })
  ],
  templateUrl: './greenhouses.component.html',
  styleUrl: './greenhouses.component.scss',
})
export class GreenhousesComponent {}
