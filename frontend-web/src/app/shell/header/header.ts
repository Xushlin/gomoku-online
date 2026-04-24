import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { CdkMenu, CdkMenuItem, CdkMenuTrigger } from '@angular/cdk/menu';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { AuthService } from '../../core/auth/auth.service';
import { LanguageService } from '../../core/i18n/language.service';
import { SUPPORTED_LOCALES, type SupportedLocale } from '../../core/i18n/supported-locales';
import { ThemeService } from '../../core/theme/theme.service';

@Component({
  selector: 'app-header',
  standalone: true,
  imports: [CdkMenu, CdkMenuItem, CdkMenuTrigger, RouterLink, TranslocoPipe],
  templateUrl: './header.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Header {
  protected readonly language = inject(LanguageService);
  protected readonly theme = inject(ThemeService);
  protected readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly locales = SUPPORTED_LOCALES;
  protected readonly currentLocaleKey = computed(() => `header.language.${this.language.current()}`);
  protected readonly currentThemeKey = computed(() => `header.theme.${this.theme.themeName()}`);
  protected readonly darkStateKey = computed(() =>
    this.theme.isDark() ? 'header.theme.dark-on' : 'header.theme.dark-off',
  );

  protected get themes(): readonly string[] {
    return this.theme.availableThemes();
  }

  protected localeKey(locale: SupportedLocale): string {
    return `header.language.${locale}`;
  }

  protected themeKey(name: string): string {
    return `header.theme.${name}`;
  }

  protected selectLocale(locale: SupportedLocale): void {
    this.language.use(locale);
  }

  protected selectTheme(name: string): void {
    this.theme.activate(name);
  }

  protected toggleDark(): void {
    this.theme.setDark(!this.theme.isDark());
  }

  protected logout(): void {
    this.auth.logout().subscribe({
      next: () => void this.router.navigateByUrl('/home'),
      error: () => void this.router.navigateByUrl('/home'),
    });
  }
}
