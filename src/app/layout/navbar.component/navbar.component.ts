
import { Component, Output, EventEmitter, signal, inject, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ChatService } from '../../core/services/chat.service';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-navbar',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './navbar.component.html',
  styleUrls: ['./navbar.component.css'],
})
export class NavbarComponent implements OnInit, OnDestroy {
  @Output() onToggle = new EventEmitter<void>();
  
  chatService = inject(ChatService);
  auth = inject(AuthService);
  
  searchQuery = signal('');
  chatMessageInput = signal('');
  
  // Timer para polling (fallback)
  private pollingInterval: any;

  ngOnInit(): void {
    // Polling a cada 10 segundos como fallback
    this.pollingInterval = setInterval(() => {
      if (!this.chatService.isOpen()) {
        this.chatService.loadUnreadCount();
      }
    }, 10000);
  }

  ngOnDestroy(): void {
    if (this.pollingInterval) {
      clearInterval(this.pollingInterval);
    }
  }

  toggle(): void {
    this.onToggle.emit();
  }

  toggleChat(): void {
    this.chatService.toggleChat();
  }

  sendMessage(): void {
    const message = this.chatMessageInput().trim();
    if (message) {
      this.chatService.sendMessage(message);
      this.chatMessageInput.set('');
    }
  }

  getAvatarUrl(user: any): string {
    if (user?.avatarUrl) return user.avatarUrl;
    const name = user?.nome || user?.userName || 'User';
    return `https://ui-avatars.com/api/?background=2563eb&color=fff&bold=true&name=${encodeURIComponent(name)}`;
  }

  formatTime(dateStr: string): string {
    const date = new Date(dateStr);
    const now = new Date();
    const diffMs = now.getTime() - date.getTime();
    const diffMins = Math.floor(diffMs / 60000);
    const diffHours = Math.floor(diffMs / 3600000);
    const diffDays = Math.floor(diffMs / 86400000);

    if (diffMins < 1) return 'agora';
    if (diffMins < 60) return `${diffMins} min`;
    if (diffHours < 24) return `${diffHours} h`;
    if (diffDays < 7) return `${diffDays} d`;
    return date.toLocaleDateString('pt-PT', { day: '2-digit', month: '2-digit' });
  }
}