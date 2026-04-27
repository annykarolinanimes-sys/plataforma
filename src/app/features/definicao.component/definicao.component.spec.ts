import { ComponentFixture, TestBed } from '@angular/core/testing';

import { DefinicaoComponent } from './definicao.component';

describe('DefinicaoComponent', () => {
  let component: DefinicaoComponent;
  let fixture: ComponentFixture<DefinicaoComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DefinicaoComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(DefinicaoComponent);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
