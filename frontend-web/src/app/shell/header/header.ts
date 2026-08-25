import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { AuthService } from '../../core/auth/auth.service';
import { LanguageService } from '../../core/i18n/language.service';
import { isSupportedLocale, SUPPORTED_LOCALES } from '../../core/i18n/supported-locales';
import { SoundService } from '../../core/sound/sound.service';
import { BoardSkinService } from '../../core/theme/board-skin.service';
import { ThemeService } from '../../core/theme/theme.service';
import {
  AppearanceMenus,
  type PickerControl,
  type ToggleControl,
} from './appearance-menus/appearance-menus';

/*
 * `PickerControl` / `ToggleControl` 的定义在 `appearance-menus` 里 —— 画它们的是那边。
 * 这里只 `import type`,所以不会把那个组件(以及它带的 `@angular/cdk`)拉进首屏:
 * 唯一的值引用在下面的 `imports` 里,而它只在 `@defer` 块里被用到,于是编译器会把它
 * 拆成一个动态 import。**而这句话是量出来的,不是推出来的**(见 tasks 的第一步)。
 */

@Component({
  selector: 'app-header',
  standalone: true,
  imports: [AppearanceMenus, RouterLink, TranslocoPipe],
  templateUrl: './header.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Header {
  protected readonly language = inject(LanguageService);
  protected readonly theme = inject(ThemeService);
  protected readonly boardSkin = inject(BoardSkinService);
  protected readonly sound = inject(SoundService);
  protected readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  /**
   * 那一组控件要不要加载了 —— 占位里任何一个 picker / Settings 被点过就置真,
   * 而 `@prefetch (on idle)` 通常已经把 chunk 拉下来了,所以这一步不等。
   */
  protected readonly menuRequested = signal(false);
  /** 点的是第几个:`0…n-1` 是内联 picker,`n` 是 Settings。加载完就开它。 */
  protected readonly pendingTrigger = signal<number | null>(null);

  /**
   * The four dropdown controls, in the order the header shows them. Both
   * placements — the inline row above `lg` and the Settings menu below it —
   * loop over this list, so a fifth control is one entry here and no template
   * edit. Options come straight from each service's registry, which is what
   * keeps "register a skin, touch no template" true.
   *
   * A getter rather than a `computed` because the registries are plain methods,
   * not signals: re-reading per change detection cannot go stale.
   */
  protected get pickers(): readonly PickerControl[] {
    return [
      {
        prefix: 'header.language',
        options: SUPPORTED_LOCALES,
        value: this.language.current(),
        hasVolume: false,
        apply: (option) => this.selectLocale(option),
      },
      {
        prefix: 'header.theme',
        options: this.theme.availableThemes(),
        value: this.theme.themeName(),
        hasVolume: false,
        apply: (option) => this.theme.activate(option),
      },
      {
        prefix: 'header.board-skin',
        options: this.boardSkin.availableSkins(),
        value: this.boardSkin.skinName(),
        hasVolume: false,
        apply: (option) => this.boardSkin.activate(option),
      },
      {
        prefix: 'header.sound-pack',
        options: this.sound.availablePacks(),
        value: this.sound.packName(),
        hasVolume: true,
        apply: (option) => this.selectSoundPack(option),
      },
    ];
  }

  /** The two switch controls, rendered after the pickers in both placements. */
  protected get toggles(): readonly ToggleControl[] {
    return [
      {
        labelKey: 'header.sound.label',
        // aria-checked tracks "has sound", i.e. the inverse of muted.
        stateKey: this.sound.muted() ? 'header.sound.off' : 'header.sound.on',
        checked: !this.sound.muted(),
        toggle: () => this.sound.setMuted(!this.sound.muted()),
      },
      {
        labelKey: 'header.theme.dark-toggle',
        stateKey: this.theme.isDark() ? 'header.theme.dark-on' : 'header.theme.dark-off',
        checked: this.theme.isDark(),
        toggle: () => this.theme.setDark(!this.theme.isDark()),
      },
    ];
  }

  private selectLocale(locale: string): void {
    // The picker list is generic over string registries, so narrow on the way
    // back in rather than casting — an unknown tag is dropped, not applied.
    if (isSupportedLocale(locale)) this.language.use(locale);
  }

  private selectSoundPack(name: string): void {
    this.sound.activate(name);
    if (!this.sound.muted()) this.sound.play('move-place');
  }

  /** 占位里的按钮:先请求加载,并记下要开哪一个。 */
  protected requestMenu(index: number): void {
    this.pendingTrigger.set(index);
    this.menuRequested.set(true);
  }

  protected logout(): void {
    this.auth.logout().subscribe({
      next: () => void this.router.navigateByUrl('/home'),
      error: () => void this.router.navigateByUrl('/home'),
    });
  }
}
