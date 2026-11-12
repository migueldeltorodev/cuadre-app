import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { Sidebar } from './sidebar';

/** App frame: side menu + routed page. Every screen of the design lives inside it. */
@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, Sidebar],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="frame">
      <app-sidebar class="frame__menu" />
      <main class="frame__content">
        <div class="frame__inner">
          <router-outlet />
        </div>
      </main>
    </div>
  `,
  styles: `
    .frame {
      min-height: 100vh;
      display: flex;
      flex-wrap: wrap;
      align-items: stretch;
    }
    .frame__menu {
      flex: 0 0 240px;
      display: flex;
    }
    .frame__content {
      flex: 999 1 560px;
      min-width: 0;
      padding: 44px 48px 80px;
    }
    .frame__inner {
      max-width: 1120px;
      margin: 0 auto;
    }
    @media (max-width: 700px) {
      .frame__menu { flex-basis: 100%; }
      .frame__content { padding: 28px 16px 64px; }
    }
  `,
})
export class Shell {}
