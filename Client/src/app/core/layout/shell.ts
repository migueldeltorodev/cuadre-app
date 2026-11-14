import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { BreadcrumbsBar } from './breadcrumbs';
import { Sidebar } from './sidebar';

/**
 * App frame: side menu + breadcrumb trail + routed page. Every screen of the design lives inside it.
 * The pages never draw their own trail: the routes say it (data.crumb) and the pages name themselves (Breadcrumbs).
 */
@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, Sidebar, BreadcrumbsBar],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="frame">
      <app-sidebar class="frame__menu" />
      <main class="frame__content">
        <div class="frame__inner">
          <app-breadcrumbs class="frame__crumbs" />
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
      flex: 0 0 248px;
      display: flex;
    }
    .frame__content {
      flex: 999 1 560px;
      min-width: 0;
      padding: 28px 48px 80px;
    }
    .frame__inner {
      max-width: 1120px;
      margin: 0 auto;
    }
    .frame__crumbs {
      min-height: 28px;
      margin: 0 0 16px -8px;
    }
    @media (max-width: 700px) {
      .frame__menu { flex-basis: 100%; }
      .frame__content { padding: 16px 16px 64px; }
    }
  `,
})
export class Shell {}
