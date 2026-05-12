// core/services/chat.service.ts
import { Injectable, signal, inject, OnDestroy } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import * as signalR from '@microsoft/signalr';
import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';

export interface ChatUser {
  id: number;
  nome: string;
  email: string;
  avatarUrl?: string;
  departamento?: string;
  cargo?: string;
}

export interface ChatMessage {
  id: number;
  fromUserId: number;
  fromUserName: string;
  toUserId: number;
  toUserName: string;
  message: string;
  isRead: boolean;
  createdAt: string;
}

export interface ChatConversation {
  userId: number;
  userName: string;
  userEmail: string;
  userAvatar?: string;
  lastMessage: string;
  lastMessageTime: string;
  unreadCount: number;
}

@Injectable({ providedIn: 'root' })
export class ChatService implements OnDestroy {
  private http = inject(HttpClient);
  private auth = inject(AuthService);
  private api = environment.apiUrl;
  private hubConnection!: signalR.HubConnection;

  // Signals
  private _isOpen = signal(false);
  private _unreadCount = signal(0);
  private _conversations = signal<ChatConversation[]>([]);
  private _currentMessages = signal<ChatMessage[]>([]);
  private _currentChatWith = signal<ChatUser | null>(null);
  private _availableUsers = signal<ChatUser[]>([]);
  private _isLoading = signal(false);

  readonly isOpen = this._isOpen.asReadonly();
  readonly unreadCount = this._unreadCount.asReadonly();
  readonly conversations = this._conversations.asReadonly();
  readonly currentMessages = this._currentMessages.asReadonly();
  readonly currentChatWith = this._currentChatWith.asReadonly();
  readonly availableUsers = this._availableUsers.asReadonly();
  readonly isLoading = this._isLoading.asReadonly();

  constructor() {
    this.startConnection();
    this.loadUnreadCount();
    this.loadConversations();
    this.loadAvailableUsers();
  }

  ngOnDestroy(): void {
    if (this.hubConnection) {
      this.hubConnection.stop();
    }
  }

  private startConnection(): void {
    const hubBaseUrl = this.api.replace(/\/api\/?$/, '');

    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(`${hubBaseUrl}/chatHub`, {
        accessTokenFactory: () => this.auth.getToken() ?? ''
      })
      .withAutomaticReconnect()
      .build();

    this.hubConnection.start()
      .then(() => {
        console.log('[Chat] Conexão SignalR estabelecida');
        this.registerHubEvents();
      })
      .catch(err => console.error('[Chat] Erro na conexão:', err));
  }

  private registerHubEvents(): void {
    this.hubConnection.on('ReceiveMessage', (message: ChatMessage) => {
      console.log('[Chat] Mensagem recebida:', message);
      
      // Adicionar à conversa atual se for com o usuário atual
      if (this._currentChatWith() && message.fromUserId === this._currentChatWith()?.id) {
        this._currentMessages.update(msgs => [...msgs, message]);
      }
      
      // Atualizar conversas e contador
      this.loadConversations();
      this.loadUnreadCount();
      
      // Emitir som de notificação
      this.playNotificationSound();
    });

    this.hubConnection.on('MessageSent', (message: ChatMessage) => {
      console.log('[Chat] Mensagem enviada:', message);
      this._currentMessages.update(msgs => [...msgs, message]);
    });

    this.hubConnection.on('MessageRead', (messageId: number) => {
      this._currentMessages.update(msgs =>
        msgs.map(m => m.id === messageId ? { ...m, isRead: true } : m)
      );
      this.loadConversations();
    });

    this.hubConnection.on('UpdateUnreadCount', () => {
      this.loadUnreadCount();
    });
  }

  toggleChat(): void {
    this._isOpen.update(v => !v);
    if (this._isOpen()) {
      this.loadConversations();
      this.loadAvailableUsers();
    }
  }

  closeChat(): void {
    this._isOpen.set(false);
    this._currentChatWith.set(null);
    this._currentMessages.set([]);
  }

  startChatWith(user: ChatUser | null): void {
    if (!user) {
      this._currentChatWith.set(null);
      this._currentMessages.set([]);
      return;
    }

    this._currentChatWith.set(user);
    this.loadMessages(user.id);
  }

  sendMessage(message: string): void {
    const currentUser = this._currentChatWith();
    if (!currentUser || !message.trim() || !this.hubConnection) return;

    this.hubConnection.invoke('SendMessage', currentUser.id, message.trim())
      .catch(err => console.error('[Chat] Erro ao enviar mensagem:', err));
  }

  markAsRead(messageId: number): void {
    if (!this.hubConnection) return;
    this.hubConnection.invoke('MarkAsRead', messageId)
      .catch(err => console.error('[Chat] Erro ao marcar como lida:', err));
  }

  markAllAsRead(userId: number): void {
    if (!this.hubConnection) return;
    this.hubConnection.invoke('MarkAllAsRead', userId)
      .catch(err => console.error('[Chat] Erro ao marcar todas como lidas:', err));
  }

  loadConversations(): void {
    this.http.get<ChatConversation[]>(`${this.api}/chat/conversations`).subscribe({
      next: (conversations) => {
        this._conversations.set(conversations);
      },
      error: (err) => console.error('[Chat] Erro ao carregar conversas:', err)
    });
  }

  loadMessages(userId: number): void {
    this._isLoading.set(true);
    this.http.get<ChatMessage[]>(`${this.api}/chat/messages/${userId}`).subscribe({
      next: (messages) => {
        this._currentMessages.set(messages);
        this._isLoading.set(false);
      },
      error: (err) => {
        console.error('[Chat] Erro ao carregar mensagens:', err);
        this._isLoading.set(false);
      }
    });
  }

  loadUnreadCount(): void {
    this.http.get<{ count: number }>(`${this.api}/chat/unread-count`).subscribe({
      next: (res) => this._unreadCount.set(res.count),
      error: (err) => console.error('[Chat] Erro ao carregar contador:', err)
    });
  }

  loadAvailableUsers(search?: string): void {
    let url = `${this.api}/chat/users`;
    if (search) {
      url += `?search=${encodeURIComponent(search)}`;
    }
    
    this.http.get<ChatUser[]>(url).subscribe({
      next: (users) => {
        const existingUserIds = this._conversations().map(c => c.userId);
        this._availableUsers.set(users.filter(u => !existingUserIds.includes(u.id)));
      },
      error: (err) => console.error('[Chat] Erro ao carregar usuários:', err)
    });
  }

  private playNotificationSound(): void {
    const audio = new Audio();
    audio.src = 'data:audio/wav;base64,U3RlYWx0aCBzb3VuZA=='; 
    audio.play().catch(() => {
    });
  }
}