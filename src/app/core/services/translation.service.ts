import { Injectable, signal, computed } from '@angular/core';

export type Language = 'pt' | 'en';

export interface TranslationKeys {
  // Login
  'login.title': string;
  'login.email': string;
  'login.password': string;
  'login.submit': string;
  'login.forgotPassword': string;
  'login.register': string;

  // Menu
  'menu.dashboard': string;
  'menu.catalog': string;
  'menu.products': string;
  'menu.clients': string;
  'menu.suppliers': string;
  'menu.vehicles': string;
  'menu.warehouses': string;
  'menu.transporters': string;
  'menu.drivers': string;
  'menu.process': string;
  'menu.reception': string;
  'menu.assignments': string;
  'menu.tripManagement': string;
  'menu.incidents': string;
  'menu.printing': string;
  'menu.invoices': string;
  'menu.admin': string;
  'menu.settings': string;

  // Admin submenu
  'admin.activities': string;
  'admin.auditLogs': string;
  'admin.usersSessions': string;

  // Settings
  'settings.title': string;
  'settings.subtitle': string;
  'settings.profile': string;
  'settings.edit': string;
  'settings.changePhoto': string;
  'settings.fullName': string;
  'settings.email': string;
  'settings.department': string;
  'settings.position': string;
  'settings.phone': string;
  'settings.role': string;
  'settings.admin': string;
  'settings.user': string;
  'settings.statistics': string;
  'settings.documents': string;
  'settings.shipments': string;
  'settings.unreadAlerts': string;
  'settings.daysRegistered': string;
  'settings.security': string;
  'settings.changePassword': string;
  'settings.logout': string;
  'settings.lastLogin': string;
  'settings.memberSince': string;
  'settings.preferences': string;
  'settings.emailNotifications': string;
  'settings.darkMode': string;
  'settings.language': string;
  'settings.portuguese': string;
  'settings.english': string;
  'settings.dragDropPhoto': string;

  // Modals
  'modal.editProfile': string;
  'modal.cancel': string;
  'modal.save': string;
  'modal.saving': string;
  'modal.changePassword': string;
  'modal.changing': string;
  'modal.currentPassword': string;
  'modal.newPassword': string;
  'modal.confirmPassword': string;

  // Messages
  'message.loading': string;
  'message.error': string;
  'message.success': string;
  'message.profileUpdated': string;
  'message.passwordChanged': string;
  'message.preferencesSaved': string;
  'message.photoUpdated': string;
  'message.uploading': string;

  // Common
  'common.undefined': string;
  'common.notDefined': string;
  'common.close': string;
  'common.save': string;
  'common.cancel': string;
  'common.edit': string;
  'common.delete': string;
  'common.view': string;
  'common.add': string;
  'common.search': string;
  'common.filter': string;
  'common.export': string;
  'common.import': string;
  'common.yes': string;
  'common.no': string;
  'common.confirm': string;
  'common.back': string;
  'common.next': string;
  'common.previous': string;
  'common.loading': string;
  'common.noData': string;
  'common.error': string;
  'common.success': string;
  'common.required': string;
  'common.mustHaveAtLeast6Chars': string;
  'common.passwordsDontMatch': string;
  'common.errorUpdatingProfile': string;
  'common.errorChangingPassword': string;
  'common.errorUploadingPhoto': string;
}

@Injectable({
  providedIn: 'root'
})
export class TranslationService {
  private currentLanguage = signal<Language>('pt');

  private translations: Record<Language, TranslationKeys> = {
    pt: {
      // Login
      'login.title': 'Entrar na Plataforma',
      'login.email': 'Email',
      'login.password': 'Palavra-passe',
      'login.submit': 'Entrar',
      'login.forgotPassword': 'Esqueceu a palavra-passe?',
      'login.register': 'Registar',

      // Menu
      'menu.dashboard': 'Dashboard',
      'menu.catalog': 'Catálogo',
      'menu.products': 'Produtos',
      'menu.clients': 'Clientes',
      'menu.suppliers': 'Fornecedores',
      'menu.vehicles': 'Veículos',
      'menu.warehouses': 'Armazém',
      'menu.transporters': 'Transportadoras',
      'menu.drivers': 'Motoristas',
      'menu.process': 'Processo',
      'menu.reception': 'Recepção',
      'menu.assignments': 'Atribuições',
      'menu.tripManagement': 'Gestão de Viagens',
      'menu.incidents': 'Incidentes',
      'menu.printing': 'Impressão',
      'menu.invoices': 'Faturas',
      'menu.admin': 'Administração',
      'menu.settings': 'Definições',

      // Admin submenu
      'admin.activities': 'Atividades',
      'admin.auditLogs': 'Logs de Auditoria',
      'admin.usersSessions': 'Utilizadores & Sessões',

      // Settings
      'settings.title': 'Definições',
      'settings.subtitle': 'Gerencie as suas informações de perfil e preferências',
      'settings.profile': 'Perfil',
      'settings.edit': 'Editar',
      'settings.changePhoto': 'Alterar foto',
      'settings.fullName': 'Nome completo',
      'settings.email': 'Email',
      'settings.department': 'Departamento',
      'settings.position': 'Cargo',
      'settings.phone': 'Telefone',
      'settings.role': 'Role',
      'settings.admin': 'Administrador',
      'settings.user': 'Utilizador',
      'settings.statistics': 'Estatísticas',
      'settings.documents': 'Documentos',
      'settings.shipments': 'Envios',
      'settings.unreadAlerts': 'Alertas não lidos',
      'settings.daysRegistered': 'Dias registado',
      'settings.security': 'Segurança',
      'settings.changePassword': 'Alterar palavra-passe',
      'settings.logout': 'Terminar sessão',
      'settings.lastLogin': 'Último login',
      'settings.memberSince': 'Membro desde',
      'settings.preferences': 'Preferências',
      'settings.emailNotifications': 'Notificações por email',
      'settings.darkMode': 'Tema escuro',
      'settings.language': 'Idioma',
      'settings.portuguese': 'Português',
      'settings.english': 'English',
      'settings.dragDropPhoto': 'Arraste a foto aqui ou clique para selecionar',

      // Modals
      'modal.editProfile': 'Editar perfil',
      'modal.cancel': 'Cancelar',
      'modal.save': 'Guardar alterações',
      'modal.saving': 'A guardar...',
      'modal.changePassword': 'Alterar palavra-passe',
      'modal.changing': 'A alterar...',
      'modal.currentPassword': 'Palavra-passe actual',
      'modal.newPassword': 'Nova palavra-passe',
      'modal.confirmPassword': 'Confirmar nova palavra-passe',

      // Messages
      'message.loading': 'A carregar dados...',
      'message.error': 'Erro',
      'message.success': 'Sucesso',
      'message.profileUpdated': 'Perfil atualizado com sucesso',
      'message.passwordChanged': 'Palavra-passe alterada com sucesso',
      'message.preferencesSaved': 'Preferências guardadas com sucesso',
      'message.photoUpdated': 'Foto atualizada com sucesso',
      'message.uploading': 'A fazer upload...',

      // Common
      'common.undefined': '—',
      'common.notDefined': 'Não definido',
      'common.close': '×',
      'common.save': 'Guardar',
      'common.cancel': 'Cancelar',
      'common.edit': 'Editar',
      'common.delete': 'Eliminar',
      'common.view': 'Ver',
      'common.add': 'Adicionar',
      'common.search': 'Pesquisar',
      'common.filter': 'Filtrar',
      'common.export': 'Exportar',
      'common.import': 'Importar',
      'common.yes': 'Sim',
      'common.no': 'Não',
      'common.confirm': 'Confirmar',
      'common.back': 'Voltar',
      'common.next': 'Seguinte',
      'common.previous': 'Anterior',
      'common.loading': 'A carregar...',
      'common.noData': 'Sem dados',
      'common.error': 'Erro',
      'common.success': 'Sucesso',
      'common.required': 'é obrigatório',
      'common.mustHaveAtLeast6Chars': 'deve ter pelo menos 6 caracteres',
      'common.passwordsDontMatch': 'As palavras-passe não coincidem',
      'common.errorUpdatingProfile': 'Erro ao atualizar perfil',
      'common.errorChangingPassword': 'Erro ao alterar palavra-passe',
      'common.errorUploadingPhoto': 'Erro ao fazer upload da foto'
    },
    en: {
      // Login
      'login.title': 'Login to Platform',
      'login.email': 'Email',
      'login.password': 'Password',
      'login.submit': 'Login',
      'login.forgotPassword': 'Forgot password?',
      'login.register': 'Register',

      // Menu
      'menu.dashboard': 'Dashboard',
      'menu.catalog': 'Catalog',
      'menu.products': 'Products',
      'menu.clients': 'Clients',
      'menu.suppliers': 'Suppliers',
      'menu.vehicles': 'Vehicles',
      'menu.warehouses': 'Warehouses',
      'menu.transporters': 'Transporters',
      'menu.drivers': 'Drivers',
      'menu.process': 'Process',
      'menu.reception': 'Reception',
      'menu.assignments': 'Assignments',
      'menu.tripManagement': 'Trip Management',
      'menu.incidents': 'Incidents',
      'menu.printing': 'Printing',
      'menu.invoices': 'Invoices',
      'menu.admin': 'Administration',
      'menu.settings': 'Settings',

      // Admin submenu
      'admin.activities': 'Activities',
      'admin.auditLogs': 'Audit Logs',
      'admin.usersSessions': 'Users & Sessions',

      // Settings
      'settings.title': 'Settings',
      'settings.subtitle': 'Manage your profile information and preferences',
      'settings.profile': 'Profile',
      'settings.edit': 'Edit',
      'settings.changePhoto': 'Change photo',
      'settings.fullName': 'Full name',
      'settings.email': 'Email',
      'settings.department': 'Department',
      'settings.position': 'Position',
      'settings.phone': 'Phone',
      'settings.role': 'Role',
      'settings.admin': 'Administrator',
      'settings.user': 'User',
      'settings.statistics': 'Statistics',
      'settings.documents': 'Documents',
      'settings.shipments': 'Shipments',
      'settings.unreadAlerts': 'Unread alerts',
      'settings.daysRegistered': 'Days registered',
      'settings.security': 'Security',
      'settings.changePassword': 'Change password',
      'settings.logout': 'Logout',
      'settings.lastLogin': 'Last login',
      'settings.memberSince': 'Member since',
      'settings.preferences': 'Preferences',
      'settings.emailNotifications': 'Email notifications',
      'settings.darkMode': 'Dark mode',
      'settings.language': 'Language',
      'settings.portuguese': 'Português',
      'settings.english': 'English',
      'settings.dragDropPhoto': 'Drag photo here or click to select',

      // Modals
      'modal.editProfile': 'Edit profile',
      'modal.cancel': 'Cancel',
      'modal.save': 'Save changes',
      'modal.saving': 'Saving...',
      'modal.changePassword': 'Change password',
      'modal.changing': 'Changing...',
      'modal.currentPassword': 'Current password',
      'modal.newPassword': 'New password',
      'modal.confirmPassword': 'Confirm new password',

      // Messages
      'message.loading': 'Loading data...',
      'message.error': 'Error',
      'message.success': 'Success',
      'message.profileUpdated': 'Profile updated successfully',
      'message.passwordChanged': 'Password changed successfully',
      'message.preferencesSaved': 'Preferences saved successfully',
      'message.photoUpdated': 'Photo updated successfully',
      'message.uploading': 'Uploading...',

      // Common
      'common.undefined': '—',
      'common.notDefined': 'Not defined',
      'common.close': '×',
      'common.save': 'Save',
      'common.cancel': 'Cancel',
      'common.edit': 'Edit',
      'common.delete': 'Delete',
      'common.view': 'View',
      'common.add': 'Add',
      'common.search': 'Search',
      'common.filter': 'Filter',
      'common.export': 'Export',
      'common.import': 'Import',
      'common.yes': 'Yes',
      'common.no': 'No',
      'common.confirm': 'Confirm',
      'common.back': 'Back',
      'common.next': 'Next',
      'common.previous': 'Previous',
      'common.loading': 'Loading...',
      'common.noData': 'No data',
      'common.error': 'Error',
      'common.success': 'Success',
      'common.required': 'is required',
      'common.mustHaveAtLeast6Chars': 'must have at least 6 characters',
      'common.passwordsDontMatch': 'Passwords do not match',
      'common.errorUpdatingProfile': 'Error updating profile',
      'common.errorChangingPassword': 'Error changing password',
      'common.errorUploadingPhoto': 'Error uploading photo'
    }
  };

  currentTranslations = computed(() => this.translations[this.currentLanguage()]);

  constructor() {
    // Load saved language preference
    const saved = localStorage.getItem('user_preferences');
    if (saved) {
      try {
        const prefs = JSON.parse(saved);
        if (prefs.language && (prefs.language === 'pt' || prefs.language === 'en')) {
          this.currentLanguage.set(prefs.language as Language);
        }
      } catch (e) {
        console.error('Error loading language preference', e);
      }
    }
  }

  setLanguage(language: Language): void {
    this.currentLanguage.set(language);
    // Update localStorage
    const saved = localStorage.getItem('user_preferences');
    if (saved) {
      try {
        const prefs = JSON.parse(saved);
        prefs.language = language;
        localStorage.setItem('user_preferences', JSON.stringify(prefs));
      } catch (e) {
        console.error('Error saving language preference', e);
      }
    }
  }

  getLanguage(): Language {
    return this.currentLanguage();
  }

  translate(key: keyof TranslationKeys): string {
    return this.currentTranslations()[key] || key;
  }

  instant(key: keyof TranslationKeys): string {
    return this.translate(key);
  }
}