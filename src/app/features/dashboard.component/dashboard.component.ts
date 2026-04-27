import { Component, signal, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { AuthService } from '../../core/services/auth.service';
import { environment } from '../../../environments/environment';



export interface AdminStatsDto {
  totalUsuariosAtivos:   number;
  totalUsuariosInativos: number;
  totalAlertas:          number;
  alertasNaoLidos:       number;
}

export interface EnvioDto {
  id:               number;
  idString:         string;
  nomeEquipamento:  string;
  dataPrevista:     string;
  estado:           string;
  usuarioId:        number;
  nomeUsuario:      string;
  dataCriacao:      string;
  dataAtualizacao:  string;
}

export interface DocumentoDto {
  id:              number;
  nome:            string;
  pathUrl:         string;
  tipo:            string;
  tamanhoBytes:    number;
  tamanhoFormatado:string;
  usuarioId:       number;
  envioId:         number | null;
  dataUpload:      string;
  dataAbertura:    string | null;
  avatarFallback?: string;
}

export interface StatCard {
  icon:    string;
  bgClass: string;
  label:   string;
  value:   string;
}

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.css'],
})
export class DashboardComponent implements OnInit {
  private http = inject(HttpClient);
  readonly auth = inject(AuthService);
  private api  = environment.apiUrl;

  searchQuery = signal('');
  isLoading   = signal(true);
  isAdmin     = this.auth.isAdmin;


  stats = signal<AdminStatsDto | null>(null);

  statCards = signal<StatCard[]>([
    { icon: 'la-chart-line',   bgClass: 'blue-bg',   label: 'Income',          value: '—'  },
    { icon: 'la-users',        bgClass: 'purple-bg', label: 'Utilizadores',    value: '—'  },
    { icon: 'la-check-circle', bgClass: 'green-bg',  label: 'Envios Pendentes',value: '—'  },
  ]);


  ngOnInit(): void {
    this.loadDashboard();
  }

  private loadDashboard(): void {
    this.isLoading.set(true);

    if (this.auth.isAdmin()) {
      this.http.get<AdminStatsDto>(`${this.api}/admin/stats`).subscribe({
        next: s => {
          this.stats.set(s);
          this.statCards.set([
            { icon: 'la-users',        bgClass: 'blue-bg',   label: 'Utilizadores Ativos',  value: String(s.totalUsuariosAtivos)  },
          ]);
        },
      });
    }
  }
}


      