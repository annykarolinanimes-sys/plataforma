const express = require('express');
const nodemailer = require('nodemailer');
const cors = require('cors');
const fs = require('fs');
const path = require('path');

const app = express();

app.use(cors());
app.use(express.json());
app.use(express.urlencoded({ extended: true }));

const transporter = nodemailer.createTransport({
  service: 'gmail',
  auth: {
    user: 'papanny412@gmail.com',
    pass: 'xgvgwnfusnkkqcbu'
  }
});

function getEmailTemplate(data) {
  let template = fs.readFileSync(
    path.join(__dirname, 'email-templates', 'contact-template.html'),
    'utf8'
  );
  
  template = template.replace(/{{MESSAGE_ID}}/g, data.messageId);
  template = template.replace(/{{DATA_COMPLETA}}/g, data.dataCompleta);
  template = template.replace(/{{FULL_NAME}}/g, data.fullName);
  template = template.replace(/{{COMPANY}}/g, data.company);
  template = template.replace(/{{EMAIL}}/g, data.email);
  template = template.replace(/{{PHONE}}/g, data.phone);
  template = template.replace(/{{FLEET_SIZE}}/g, data.fleetSize);
  template = template.replace(/{{MESSAGE}}/g, data.message);

  return template;
}

app.post('/api/contact', (req, res) => {
  const { fullName, name, email, company, phone, fleetSize, message } = req.body;

  if (!email || !message) {
    return res.status(400).json({
      success: false,
      error: 'Por favor, preencha todos os campos obrigatórios (E-mail e Mensagem).'
    });
  }

  const agora = new Date();
  const dataFormatada = agora.toLocaleDateString('pt-PT');
  const dataCompleta = agora.toLocaleString('pt-PT');
  const messageId = 'ACCU-' + Math.random().toString(36).substring(2, 11).toUpperCase();

  const dadosTemplate = {
    messageId: messageId,
    dataCompleta: dataCompleta,
    fullName: (fullName || name || 'Não informado').trim(),
    company: (company || 'Não informada').trim(),
    email: email.trim(),
    phone: (phone || 'Não informado').trim(),
    fleetSize: (fleetSize || 'Não especificado').trim(),
    message: message.trim()
  };

  const htmlContent = getEmailTemplate(dadosTemplate);

  const textContent = `
==================================================
        NOVO CONTACTO DE SUPORTE - CORTEXLOG
==================================================
ID da Mensagem: ${dadosTemplate.messageId}
Data de Envio: ${dadosTemplate.dataCompleta}

INFORMAÇÕES DO CLIENTE:
- Nome: ${dadosTemplate.fullName}
- Empresa: ${dadosTemplate.company}
- E-mail: ${dadosTemplate.email}
- Telefone: ${dadosTemplate.phone}
- Frota: ${dadosTemplate.fleetSize}

MENSAGEM:
${dadosTemplate.message}
==================================================
  `;

  const mailOptions = {
    from: `"Suporte CortexLog" <papanny412@gmail.com>`,
    to: 'papanny412@gmail.com', 
    subject: ` NOVO CONTACTO de ${dadosTemplate.fullName} - ${dataFormatada}`,
    text: textContent,
    html: htmlContent,
    replyTo: dadosTemplate.email 
  };


  transporter.sendMail(mailOptions, (error, info) => {
    if (error) {
      console.error('Erro ao enviar email:', error);
      return res.status(500).json({
        success: false,
        error: 'Ocorreu um erro ao enviar a sua mensagem. Por favor, tente novamente mais tarde.'
      });
    }

    console.log(`[ENVIADO] Contacto de ${dadosTemplate.fullName} (${dadosTemplate.email}) - ID: ${messageId}`);
    
    res.status(200).json({
      success: true,
      message: 'Mensagem enviada com sucesso. Entraremos em contacto em breve.'
    });
  });
});



app.get('/api/health', (req, res) => {
  res.status(200).json({
    status: 'online',
    service: 'AccuSoft Email Service',
    version: '1.0.0',
    timestamp: new Date().toISOString()
  });
});

const PORT = process.env.PORT || 3000;
app.listen(PORT, () => {
  console.log(`
========================================
    CORTXLOG - SERVIDOR DE SUPORTE      

Servidor online e funcional!
Rota da API: http://localhost:${PORT}/api/contact
Status de Saúde: http://localhost:${PORT}/api/health
========================================
  `);
});