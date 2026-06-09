 import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-footer',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './footer.component.html',
  styleUrls: ['./footer.component.css'],
})
export class FooterComponent {
  readonly ano = new Date().getFullYear();

  readonly externalLinks = [
    { label: 'Política de Privacidade', url: 'http://localhost:5500/privacidade.html', external: true },
    { label: 'Termos de Utilização', url: 'http://localhost:5500/termos.html', external: true },
    { label: 'Suporte', url: 'http://localhost:5500/#contacto', external: true },
    { label: 'Site Institucional', url: 'http://localhost:5500', external: true },
  ];

}