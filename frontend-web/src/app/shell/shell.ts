import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { Header } from './header/header';

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [RouterOutlet, Header],
  templateUrl: './shell.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Shell {}
