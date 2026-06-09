document.addEventListener('DOMContentLoaded', () => {

  const privacyOverlay = document.getElementById('privacyOverlay');
  const confirmBtn = document.getElementById('confirmChoices');
  const rejectAllBtn = document.getElementById('rejectAll');
  const cookieFloatingBtn = document.getElementById('cookieFloatingBtn');
  const cookieToggles = document.querySelectorAll('.cookie-toggle');
  const COOKIE_CONSENT_KEY = 'cortexlog_cookie_consent_accepted';

  const defaultPreferences = {
    strictlyNecessary: true,   
    performance: true,         
    functionality: false,      
    advertising: false         
  };

  const hasAccepted = localStorage.getItem(COOKIE_CONSENT_KEY);

  if (hasAccepted === 'true') {
    if (privacyOverlay) {
      privacyOverlay.classList.add('hidden');
      document.body.classList.remove('modal-open');
    }
    if (cookieFloatingBtn) {
      cookieFloatingBtn.classList.remove('hidden');
    }
    const savedPrefs = localStorage.getItem('cortexlog_cookie_preferences');
    if (savedPrefs) {
      try {
        const prefs = JSON.parse(savedPrefs);
        applyPreferencesToToggles(prefs);
      } catch (e) {}
    }
  } else {
    if (privacyOverlay) {
      privacyOverlay.classList.remove('hidden');
      document.body.classList.add('modal-open');
    }
    if (cookieFloatingBtn) {
      cookieFloatingBtn.classList.add('hidden');
    }
  }

  function openPrivacyModal() {
    if (privacyOverlay) {
      privacyOverlay.classList.remove('hidden');
      document.body.classList.add('modal-open');
    }
  }

  function closePrivacyModal() {
    if (privacyOverlay) {
      privacyOverlay.classList.add('hidden');
      document.body.classList.remove('modal-open');
    }
  }

  function applyPreferencesToToggles(prefs) {
    cookieToggles.forEach(toggle => {
      const category = toggle.dataset.category;
      if (category === 'functionality') {
        toggle.checked = prefs.functionality || false;
      } else if (category === 'advertising') {
        toggle.checked = prefs.advertising || false;
      }
    });
  }

  function getCurrentPreferences() {
    const prefs = { ...defaultPreferences };
    cookieToggles.forEach(toggle => {
      const category = toggle.dataset.category;
      if (category === 'functionality') {
        prefs.functionality = toggle.checked;
      } else if (category === 'advertising') {
        prefs.advertising = toggle.checked;
      }
    });
    return prefs;
  }

  if (confirmBtn) {
    confirmBtn.addEventListener('click', () => {
      const prefs = getCurrentPreferences();
      localStorage.setItem(COOKIE_CONSENT_KEY, 'true');
      localStorage.setItem('cortexlog_cookie_preferences', JSON.stringify(prefs));
      closePrivacyModal();
      if (cookieFloatingBtn) {
        cookieFloatingBtn.classList.remove('hidden');
      }
    });
  }

  if (rejectAllBtn) {
    rejectAllBtn.addEventListener('click', () => {
      cookieToggles.forEach(toggle => {
        toggle.checked = false;
      });
      closePrivacyModal();
      if (cookieFloatingBtn) {
        cookieFloatingBtn.classList.add('hidden');
      }
    });
  }

  if (cookieFloatingBtn) {
    cookieFloatingBtn.addEventListener('click', () => {
      const savedPrefs = localStorage.getItem('cortexlog_cookie_preferences');
      if (savedPrefs) {
        try {
          const prefs = JSON.parse(savedPrefs);
          applyPreferencesToToggles(prefs);
        } catch (e) {}
      }
      openPrivacyModal();
    });
  }

  if (privacyOverlay) {
    privacyOverlay.addEventListener('click', (e) => {
      if (e.target === privacyOverlay && hasAccepted === 'true') {
        closePrivacyModal();
      }
    });
  }

  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape' && privacyOverlay && !privacyOverlay.classList.contains('hidden')) {
      if (hasAccepted === 'true') {
        closePrivacyModal();
      }
    }
  });


  const allButtons = document.querySelectorAll('button, .btn, .btn-confirm, .btn-reject-all');
  
  allButtons.forEach(btn => {
    btn.addEventListener('mousedown', function() {
      this.style.transform = 'scale(0.96)';
    });
    
    btn.addEventListener('mouseup', function() {
      this.style.transform = '';
    });
    
    btn.addEventListener('mouseleave', function() {
      this.style.transform = '';
    });
  });

  const navLinks = document.querySelectorAll('.nav-links a[href^="#"]');
  
  navLinks.forEach(link => {
    link.addEventListener('click', function(e) {
      const targetId = this.getAttribute('href');
      if (targetId === '#') return;
      
      const targetElement = document.querySelector(targetId);
      if (targetElement) {
        e.preventDefault();
        targetElement.scrollIntoView({
          behavior: 'smooth',
          block: 'start'
        });
      }
    });
  });

  console.log('🚛 CortexLog — Todas as funcionalidades carregadas com sucesso.');


const contactForm = document.getElementById('contactForm');
const submitBtn = document.getElementById('submitBtn');
const btnText = submitBtn?.querySelector('.btn-text');
const btnLoader = submitBtn?.querySelector('.btn-loader');
const btnSuccess = submitBtn?.querySelector('.btn-success');
const toast = document.getElementById('toastNotification');

function showToast(message, isError = false) {
  if (!toast) return;
  
  const toastContent = toast.querySelector('.toast-content');
  const toastIcon = toastContent.querySelector('i');
  const toastSpan = toastContent.querySelector('span');
  
  if (isError) {
    toastIcon.className = 'las la-exclamation-triangle';
    toastIcon.style.color = '#ef4444';
  } else {
    toastIcon.className = 'las la-check-circle';
    toastIcon.style.color = '#10b981';
  }
  
  toastSpan.textContent = message;
  toast.classList.remove('hidden');
  
  setTimeout(() => {
    toast.classList.add('hidden');
  }, 4000);
}

function showFieldError(fieldId, message) {
  const errorSpan = document.getElementById(`${fieldId}Error`);
  if (errorSpan) {
    errorSpan.textContent = message;
    errorSpan.classList.add('show');
  }
  const input = document.getElementById(fieldId);
  if (input) {
    input.classList.add('input-error');
  }
}

function clearFieldError(fieldId) {
  const errorSpan = document.getElementById(`${fieldId}Error`);
  if (errorSpan) {
    errorSpan.classList.remove('show');
    errorSpan.textContent = '';
  }
  const input = document.getElementById(fieldId);
  if (input) {
    input.classList.remove('input-error');
  }
}

function clearAllErrors() {
  const fields = ['fullName', 'email', 'message', 'privacyConsent'];
  fields.forEach(field => clearFieldError(field));
}

function validateForm() {
  let isValid = true;
  clearAllErrors();
  
  const fullName = document.getElementById('fullName')?.value.trim();
  const email = document.getElementById('email')?.value.trim();
  const message = document.getElementById('message')?.value.trim();
  const privacyConsent = document.getElementById('privacyConsent')?.checked;
  
  if (!fullName) {
    showFieldError('fullName', 'Por favor, insira o seu nome completo.');
    isValid = false;
  } else if (fullName.length < 3) {
    showFieldError('fullName', 'O nome deve ter pelo menos 3 caracteres.');
    isValid = false;
  }
  
  const emailRegex = /^[^\s@]+@([^\s@]+\.)+[^\s@]+$/;
  if (!email) {
    showFieldError('email', 'Por favor, insira o seu e-mail.');
    isValid = false;
  } else if (!emailRegex.test(email)) {
    showFieldError('email', 'Por favor, insira um e-mail válido (ex: nome@empresa.com).');
    isValid = false;
  }
  
  if (!message) {
    showFieldError('message', 'Por favor, escreva a sua mensagem.');
    isValid = false;
  } else if (message.length < 10) {
    showFieldError('message', 'A mensagem deve ter pelo menos 10 caracteres.');
    isValid = false;
  }
  
  if (!privacyConsent) {
    showFieldError('consent', 'Deve aceitar a Política de Privacidade para enviar a mensagem.');
    isValid = false;
  }
  
  return isValid;
}

async function submitForm(formData) {
  try {
    const response = await fetch('http://localhost:3000/api/contact', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(Object.fromEntries(formData))
    });

    const data = await response.json();

    if (!response.ok) {
      throw new Error(data.error || 'Erro ao enviar mensagem');
    }

    return { success: true, message: data.message };
  } catch (error) {
    console.error('Erro no envio:', error);
    return { success: false, error: error.message };
  }
}

if (contactForm) {
  contactForm.addEventListener('submit', async (e) => {
    e.preventDefault();
    
    if (!validateForm()) {
      showToast('Por favor, corrija os erros no formulário.', true);
      return;
    }
    
    submitBtn.disabled = true;
    btnText.classList.add('hidden');
    btnLoader.classList.remove('hidden');
    
    const formData = new FormData();
    formData.append('fullName', document.getElementById('fullName').value.trim());
    formData.append('company', document.getElementById('company')?.value.trim() || '');
    formData.append('email', document.getElementById('email').value.trim());
    formData.append('phone', document.getElementById('phone')?.value.trim() || '');
    formData.append('fleetSize', document.getElementById('fleetSize')?.value || '');
    formData.append('message', document.getElementById('message').value.trim());
    formData.append('timestamp', new Date().toISOString());
    
    try {
      const result = await submitForm(formData);
      
      if (result.success) {
        btnLoader.classList.add('hidden');
        btnSuccess.classList.remove('hidden');
        
        showToast('Mensagem enviada com sucesso! Entraremos em contacto em breve.');
        
        contactForm.reset();
        
        setTimeout(() => {
          btnSuccess.classList.add('hidden');
          btnText.classList.remove('hidden');
          submitBtn.disabled = false;
        }, 3000);
      }
    } catch (error) {
      console.error('Erro ao enviar:', error);
      btnLoader.classList.add('hidden');
      btnText.classList.remove('hidden');
      submitBtn.disabled = false;
      showToast('Ocorreu um erro. Por favor, tente novamente mais tarde.', true);
    }
  });
  
  const inputs = ['fullName', 'email', 'message'];
  inputs.forEach(id => {
    const input = document.getElementById(id);
    if (input) {
      input.addEventListener('input', () => clearFieldError(id));
    }
  });
  
  const privacyCheckbox = document.getElementById('privacyConsent');
  if (privacyCheckbox) {
    privacyCheckbox.addEventListener('change', () => clearFieldError('consent'));
  }
}

const ctaButton = document.querySelector('.hero-main-btn');
if (ctaButton) {
  ctaButton.addEventListener('click', (e) => {
    e.preventDefault();
    const contactSection = document.getElementById('contacto');
    if (contactSection) {
      contactSection.scrollIntoView({ behavior: 'smooth' });
    }
  });
}
});