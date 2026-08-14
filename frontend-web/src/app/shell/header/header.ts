import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { CdkMenu, CdkMenuItem, CdkMenuItemCheckbox, CdkMenuTrigger } from '@angular/cdk/menu';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { AuthService } from '../../core/auth/auth.service';
import { LanguageService } from '../../core/i18n/language.service';
import { isSupportedLocale, SUPPORTED_LOCALES } from '../../core/i18n/supported-locales';
import { SoundService } from '../../core/sound/sound.service';
import { BoardSkinService } from '../../core/theme/board-skin.service';
import { ThemeService } from '../../core/theme/theme.service';

/**
 * One dropdown appearance control. Every string derives from `prefix`: the
 * control's name is `<prefix>.label` and the current value plus each option
 * is `<prefix>.<option>`. Language, theme, board skin and sound pack are all
 * this shape, so the template renders them from one loop.
 */
interface PickerControl {
  readonly prefix: string;
  readonly options: readonly string[];
  readonly value: string;
  /** The sound-pack menu carries the volume slider under its options. */
  readonly hasVolume: boolean;
  readonly apply: (option: string) => void;
}

/** One two-state appearance control — sound on/off, dark mode on/off. */
interface ToggleControl {
  readonly labelKey: string;
  readonly stateKey: string;
  readonly checked: boolean;
  readonly toggle: () => void;
}

@Component({
  selector: 'app-header',
  standalone: true,
  imports: [CdkMenu, CdkMenuItem, CdkMenuItemCheckbox, CdkMenuTrigger, RouterLink, TranslocoPipe],
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

  protected onVolumeChange(value: string): void {
    this.sound.setVolume(Number(value));
    // Audition the new level on release so the user hears what they chose
    // (mirrors the pack-switch audition; silent when muted or at 0).
    if (!this.sound.muted()) this.sound.play('move-place');
  }

  protected logout(): void {
    this.auth.logout().subscribe({
      next: () => void this.router.navigateByUrl('/home'),
      error: () => void this.router.navigateByUrl('/home'),
    });
  }
}
