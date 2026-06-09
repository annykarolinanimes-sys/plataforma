import { Component, signal, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterOutlet } from '@angular/router';
import { SidebarComponent } from '../sidebar.component/sidebar.component';
import { NavbarComponent } from '../navbar.component/navbar.component';
import { FooterComponent } from '../footer.component/footer.component';

@Component({
  selector: 'app-main-layout.component',
  standalone: true,
  imports: [CommonModule, RouterOutlet, SidebarComponent, NavbarComponent,FooterComponent,],
  templateUrl: './main-layout.component.html',
  styleUrl: './main-layout.component.css',
})
export class MainLayoutComponent {
  isSidebarCollapsed = signal(false);

  toggleSidebar(): void {
    this.isSidebarCollapsed.update(v => !v);
  }

  @HostListener('window:resize', ['$event'])
  onResize(event: Event): void {
    const width = (event.target as Window).innerWidth;
    if (width < 768 && !this.isSidebarCollapsed()) {
      this.isSidebarCollapsed.set(true);
    }
  }
}
