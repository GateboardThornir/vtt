import type { en } from './en'

/**
 * Italian strings.
 *
 * Typed against the English set, so a key added there and forgotten here is a compile error rather
 * than a sentence that silently falls back to English in the middle of an Italian page.
 */
export const it: typeof en = {
  common: {
    appName: 'VTT',
    signOut: 'Esci',
    loading: 'Caricamento…',
    language: 'Lingua',
    unexpectedError: 'Qualcosa è andato storto. Riprova.',
  },
  signIn: {
    title: 'Accedi',
    username: 'Nome utente',
    password: 'Password',
    submit: 'Accedi',
    invalidCredentials: 'Nome utente e password non corrispondono.',
    accountDisabled: 'Questo account è stato disabilitato. Rivolgiti a un amministratore.',
  },
  pending: {
    title: 'In attesa di approvazione',
    body: "Il tuo account esiste, ma un amministratore non l'ha ancora approvato. Potrai accedere appena lo farà.",
  },
  register: {
    title: 'Crea il tuo account',
    intro: 'Sei stato invitato. Scegli un nome utente e una password.',
    username: 'Nome utente',
    password: 'Password',
    submit: 'Crea account',
    missingToken: "A questo link manca l'invito. Chiedine uno nuovo a chi ti ha invitato.",
    success: 'Il tuo account è stato creato ed è in attesa di approvazione da un amministratore.',
    errors: {
      invite_invalid: 'Questo invito non è valido. Chiedine uno nuovo.',
      invite_expired: 'Questo invito è scaduto. Chiedine uno nuovo.',
      invite_already_used: 'Questo invito è già stato usato.',
      username_taken: 'Questo nome utente è già in uso.',
      username_invalid:
        'Il nome utente ha da 3 a 32 caratteri, tra lettere, cifre, trattino e trattino basso.',
      password_too_short: 'La password deve avere almeno 12 caratteri.',
    },
  },
  admin: {
    title: 'Account',
    pendingTitle: 'In attesa di approvazione',
    noPending: 'Nessuno in attesa.',
    approve: 'Approva',
    reject: 'Rifiuta',
    disable: 'Disabilita',
    enable: 'Riabilita',
    username: 'Nome utente',
    state: 'Stato',
    role: 'Ruolo',
    registered: 'Registrato',
    invites: {
      title: 'Inviti',
      create: 'Crea un invito',
      linkReady: 'Manda questo link a chi vuoi invitare. Viene mostrato una sola volta e non è recuperabile.',
      expires: 'Scade {{when}}',
      copy: 'Copia',
    },
    states: {
      Pending: 'In attesa',
      Active: 'Attivo',
      Disabled: 'Disabilitato',
    },
    roles: {
      Member: 'Membro',
      Admin: 'Amministratore',
    },
  },
  campaigns: {
    title: 'Campagne',
    none: 'Non fai ancora parte di nessuna campagna.',
    create: 'Crea una campagna',
    name: 'Nome',
    system: 'Sistema di gioco',
    version: 'Versione',
    submit: 'Crea',
    open: 'Apri',
    roster: 'Partecipanti',
    invite: 'Invita qualcuno',
    inviteUsername: 'Nome utente',
    inviteSubmit: 'Invita',
    remove: 'Rimuovi',
    leave: 'Lascia questa campagna',
    notFound: 'Questa campagna non esiste, oppure non ne fai parte.',
    invitations: 'Inviti',
    accept: 'Accetta',
    decline: 'Rifiuta',
    noSuchAccount: 'Non esiste un account attivo con questo nome.',
    alreadyOnRoster: 'Questa persona fa già parte dei partecipanti.',
    roles: { Master: 'Master', Player: 'Giocatore' },
    states: { Invited: 'Invitato', Active: 'Attivo', Declined: 'Rifiutato', Left: 'Uscito' },
  },
  notifications: {
    title: 'Notifiche',
    none: 'Niente di nuovo.',
    markAllRead: 'Segna tutte come lette',
    unread: '{{count}} da leggere',
    CampaignInvitation: 'Sei stato invitato a {{subject}}.',
    AccountApproved: 'Il tuo account è stato approvato.',
    AccountRejected: 'Il tuo account non è stato approvato.',
  },
  home: {
    signedInAs: 'Accesso eseguito come {{username}}',
    campaignsLink: 'Campagne',
    adminLink: 'Gestisci account',
  },
}
