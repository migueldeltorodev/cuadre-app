import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/** Temporary page for menu entries whose feature slice is not built yet. */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1 class="page-title">{{ name() }}</h1>
    <p class="page-subtitle">Esta pantalla llega en una de las próximas entregas.</p>
  `,
})
export default class PlaceholderPage {
  /** Bound from the route's `data.name` (withComponentInputBinding). */
  readonly name = input.required<string>();
}
