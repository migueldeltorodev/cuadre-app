import { ChangeDetectionStrategy, Component, Injectable, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRouteSnapshot, NavigationEnd, NavigationStart, Router, RouterLink } from '@angular/router';
import { filter } from 'rxjs';

/** One step of the trail. Without a link it is the current page. */
export interface Crumb {
  label: string;
  link?: string | readonly unknown[];
}

/**
 * The breadcrumb trail of the page on screen.
 *
 * The static part comes from the routes: every route with `data: { crumb: 'Productos' }` adds one step, linked to
 * its own URL. A page whose name depends on data (a product, a closing...) calls `set()` once it has it: that label
 * replaces the last step, and `parents` adds steps before it (e.g. the product above "Editar").
 * The override is cleared on every navigation, so a page never leaks its label into the next one.
 */
@Injectable({ providedIn: 'root' })
export class Breadcrumbs {
  private readonly router = inject(Router);

  private readonly base = signal<Crumb[]>([]);
  private readonly override = signal<{ label: string; parents: Crumb[] } | null>(null);

  /** The whole trail, "Inicio" first. Empty on the home page. */
  readonly trail = computed<Crumb[]>(() => {
    const base = this.base();
    if (base.length === 0) {
      return [];
    }

    const override = this.override();
    const steps = override
      ? [...base.slice(0, -1), ...override.parents, { label: override.label }]
      : [...base.slice(0, -1), { label: base[base.length - 1].label }];
    return [{ label: 'Inicio', link: '/' }, ...steps];
  });

  constructor() {
    // Created lazily by the shell: the first navigation may already be over.
    this.base.set(fromSnapshot(this.router.routerState.snapshot.root));
    this.router.events
      .pipe(
        filter((event) => event instanceof NavigationStart || event instanceof NavigationEnd),
        takeUntilDestroyed(),
      )
      .subscribe((event) => {
        if (event instanceof NavigationStart) {
          this.override.set(null);
        } else {
          this.base.set(fromSnapshot(this.router.routerState.snapshot.root));
        }
      });
  }

  /** The real name of the current page (and, optionally, the steps above it that the routes cannot know). */
  set(label: string, parents: Crumb[] = []): void {
    this.override.set({ label, parents });
  }
}

/**
 * Walks the active routes from the root. Reads the crumb from the route CONFIG, not from `snapshot.data`:
 * empty-path children inherit their parent's data and would repeat its step.
 */
function fromSnapshot(root: ActivatedRouteSnapshot): Crumb[] {
  const crumbs: Crumb[] = [];
  const segments: string[] = [];
  let route: ActivatedRouteSnapshot | null = root;

  while (route) {
    segments.push(...route.url.map((segment) => segment.path));
    const label = route.routeConfig?.data?.['crumb'] as string | undefined;
    if (label) {
      const link = '/' + segments.join('/');
      // A redirect child ('' → 'stock') can point at the same URL as its parent: keep one step.
      if (crumbs.at(-1)?.link !== link) {
        crumbs.push({ label, link });
      }
    }
    route = route.firstChild;
  }

  return crumbs;
}

/** The trail on top of every page (except Inicio). The last step is the page itself, not a link. */
@Component({
  selector: 'app-breadcrumbs',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (crumbs.trail(); as trail) {
      @if (trail.length > 0) {
        <nav class="crumbs" aria-label="Ruta">
          <ol class="crumbs__list">
            @for (crumb of trail; track $index; let last = $last) {
              <li class="crumbs__item">
                @if (crumb.link && !last) {
                  <a class="crumbs__link" [routerLink]="crumb.link">
                    @if ($first) {
                      <svg class="crumbs__home" width="15" height="15" viewBox="0 0 20 20" fill="none"
                        stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round"
                        aria-hidden="true">
                        <path d="M3 9.5L10 4l7 5.5V16a1 1 0 0 1-1 1h-4v-5H8v5H4a1 1 0 0 1-1-1z" />
                      </svg>
                    }
                    <span [class.visually-hidden]="$first">{{ crumb.label }}</span>
                  </a>
                  <svg class="crumbs__sep" width="14" height="14" viewBox="0 0 20 20" fill="none"
                    stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"
                    aria-hidden="true">
                    <path d="M8 5l5 5-5 5" />
                  </svg>
                } @else {
                  <span class="crumbs__current" aria-current="page">{{ crumb.label }}</span>
                }
              </li>
            }
          </ol>
        </nav>
      }
    }
  `,
  styles: `
    :host { display: block; }
    .crumbs__list {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 4px 2px;
      margin: 0;
      padding: 0;
      list-style: none;
      font-size: 13.5px;
    }
    .crumbs__item { display: flex; align-items: center; gap: 2px; min-width: 0; }
    .crumbs__link {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      min-height: 28px;
      padding: 0 8px;
      border-radius: var(--r-pill);
      color: var(--c-muted);
      text-decoration: none;
      transition: background-color 0.15s, color 0.15s;
    }
    .crumbs__item:first-child .crumbs__link { padding: 0 6px; }
    .crumbs__link:hover { background: var(--c-chip); color: var(--c-ink); }
    .crumbs__link:focus-visible { outline: 2px solid var(--c-accent); outline-offset: 1px; }
    .crumbs__home { flex: none; }
    .crumbs__sep { flex: none; color: #c4bcae; }
    .crumbs__current {
      padding: 0 8px;
      color: var(--c-ink);
      font-weight: 500;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
      max-width: 40ch;
    }
    .visually-hidden {
      position: absolute;
      width: 1px;
      height: 1px;
      overflow: hidden;
      clip: rect(0 0 0 0);
      white-space: nowrap;
    }
  `,
})
export class BreadcrumbsBar {
  protected readonly crumbs = inject(Breadcrumbs);
}
