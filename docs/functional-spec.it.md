# Piattaforma per Giochi di Ruolo da Tavolo — Specifica Funzionale

**Stato:** Baseline v1.0 — consolida tutte le decisioni prese durante le interviste di raccolta requisiti e di architettura (luglio 2026).
**Scopo:** Questo è il documento fondativo del progetto. Registra cos'è la piattaforma, cosa deve fare, e i vincoli entro cui verrà costruita. I dettagli architetturali oltre il riepilogo del §10 vivono in un documento di architettura separato.

---

## 1. Visione e sintesi del prodotto

Una piattaforma web privata, ad accesso solo su invito, per giocare a giochi di ruolo da tavolo dal vivo via internet. È un **virtual tabletop (VTT)** completo: i gruppi si riuniscono in tempo reale attorno a un tavolo di gioco digitale condiviso con mappe di battaglia, pedine (token), nebbia di guerra, dadi, schede personaggio, voce e video — con la piattaforma stessa che comprende e automatizza le regole del sistema di gioco in uso.

La piattaforma è **multi-sistema per progettazione**. Dungeons & Dragons 5ª edizione è il primo sistema supportato, costruito sui contenuti dell'SRD a licenza libera, ma il nucleo della piattaforma non fa alcuna assunzione su un gioco specifico: la conoscenza delle regole è incapsulata in *moduli di sistema* intercambiabili, sviluppati e distribuiti dal mantenitore della piattaforma.

Il pubblico previsto è una cerchia chiusa di utenti fidati — un gruppo di amici sotto le cinquanta persone — con l'iscrizione controllata da un amministratore. Non è un prodotto pubblico e non è progettata per scalare oltre questa comunità.

## 2. Ambito

**Incluso nell'ambito:** gestione degli account con approvazione da parte dell'amministratore; gestione di campagne e sessioni con pianificazione e RSVP; un tavolo di gioco dal vivo, server-autoritativo, con mappe, token, nebbia di guerra, tracciamento dell'iniziativa, dadi e automazione delle regole per sistema; schede personaggio strutturate con calcolo dei valori derivati; compendi (SRD incluso più contenuti caricati dal Master); materiali condivisi con controllo di visibilità per singolo giocatore; chat testuale integrata, voce/video peer-to-peer, e audio ambientale sincronizzato controllato dal GM; hosting di mappe, token, immagini, documenti PDF e file audio; un'interfaccia bilingue (italiano/inglese).

**Escluso dall'ambito:** registrazione o scopribilità pubblica; gioco asincrono per corrispondenza (play-by-post); un editor integrato che permetta agli utenti di creare nuovi sistemi di gioco; ridistribuzione di contenuti coperti da copyright (non-SRD) dei manuali; utilizzo del tavolo dal vivo su smartphone; registrazione delle sessioni; hosting di clip video; qualsiasi flusso basato su email (non viene raccolto alcun indirizzo email).

## 3. Utenti, ruoli e accessi

Esistono due distinzioni a livello di account e un ruolo a livello di campagna.

**Amministratore.** Un account a livello di piattaforma che approva o rifiuta le registrazioni, gestisce gli account utente ed esegue il recupero degli account. L'amministratore non ha accesso privilegiato ai contenuti delle campagne in virtù del proprio ruolo.

**Membro.** Ogni utente approvato. L'appartenenza in sé non comporta alcun ruolo di gioco: **qualsiasi membro può creare una campagna, e crearla lo rende Master (Game Master) di quella campagna**. La stessa persona può quindi essere Master in una campagna e Giocatore in un'altra. Master e Giocatore sono ruoli *interni a una campagna*, non tipi di account.

**Master (per campagna).** Controllo completo sulla propria campagna: roster, sessioni, scene e mappe, tutti i contenuti e la loro visibilità, tutti i personaggi, e il tavolo dal vivo. Il Master dispone di uno spazio di preparazione privato "dietro le quinte", invisibile ai Giocatori finché il materiale non viene deliberatamente rivelato.

**Giocatore (per campagna).** Entra nel roster di una campagna su invito del suo Master, gestisce la propria scheda personaggio (sotto la supervisione del Master), visualizza i materiali che il Master ha reso visibili a lui/lei, partecipa alle sessioni, e interagisce con il tavolo entro i permessi concessi dal Master e dal sistema di gioco attivo.

### 3.1 Registrazione e ciclo di vita dell'account

La registrazione è possibile solo tramite un URL di invito dedicato (un token monouso generato dall'amministratore). Il richiedente sceglie un nome utente e una password — **non viene richiesto né memorizzato alcun indirizzo email** — e l'account entra in uno stato di *attesa* finché l'amministratore non lo approva o rifiuta esplicitamente. Solo gli account approvati possono accedere.

### 3.2 Recupero dell'account

Poiché non esiste alcuna email, il recupero è mediato dall'amministratore: l'amministratore genera un **codice di reset monouso**, lo consegna all'utente fuori banda (di persona, tramite app di messaggistica), e l'utente lo utilizza per impostare una nuova password. L'amministratore non vede, non imposta e non conosce mai la password di un utente in nessun momento. Anche il recupero del nome utente dimenticato viene risolto allo stesso modo, con l'amministratore che ricerca l'account.

### 3.3 Notifiche

Tutte le notifiche (inviti a campagne, pianificazione delle sessioni, richieste di RSVP, esiti delle approvazioni) sono **solo in-app**, come diretta conseguenza del non raccogliere alcun indirizzo email.

## 4. Campagne e sessioni

Una **campagna** è l'unità di gioco a lungo termine: ha un Master, un roster di Giocatori, un sistema di gioco assegnato (e una versione del sistema — vedi §8), uno stato del mondo persistente, la propria libreria di contenuti, e la propria cronologia delle sessioni. Lo stato della campagna persiste indefinitamente tra una sessione e l'altra; nulla si azzera tra una data di gioco e l'altra.

Una **sessione** è un evento dal vivo pianificato all'interno di una campagna. Le sessioni ereditano automaticamente il roster della campagna — i Giocatori non vengono reinvitati ogni volta — e ogni sessione pianificata prevede un **RSVP per singola sessione**, in modo che il Master sappia chi parteciperà. Le sessioni sono **solo dal vivo e sincrone**: il tavolo è attivo mentre una sessione è in corso, e non esiste alcuna modalità di gioco asincrona.

Lo spazio di preparazione del Master appartiene alla campagna, non alla sessione: scene, mappe, incontri e materiali possono essere preparati in anticipo in qualsiasi momento e rivelati durante il gioco.

## 5. Il tavolo di gioco dal vivo

Il tavolo è il cuore in tempo reale della piattaforma. Durante una sessione attiva, tutti i partecipanti condividono una vista sincronizzata governata da queste regole funzionali:

**Mappa di battaglia e token.** Il Master presenta scene costruite su immagini di mappe caricate, con token mobili che rappresentano personaggi, mostri e oggetti. I Giocatori possono muovere i token di loro proprietà; il Master può muovere qualsiasi cosa.

**Nebbia di guerra.** Il Master controlla quali aree della mappa ciascun Giocatore può vedere. Le regioni nascoste, i token nascosti e la preparazione non ancora rivelata **non devono mai essere trasmessi ai client dei Giocatori** — la visibilità è una proprietà di sicurezza imposta lato server, non una scelta di visualizzazione lato client (vedi §9).

**Iniziativa e turni.** Il combattimento utilizza un **tracciatore di iniziativa consultivo**: il tavolo mostra l'ordine dei turni in modo prominente, ma non impedisce meccanicamente agli altri partecipanti di agire. L'ordine è una convenzione sociale condivisa supportata dal software, non imposta da esso.

**Dadi.** Un tiratore di dadi integrato con la chat e con le schede personaggio, che supporta la semantica dei dadi del sistema di gioco attivo, con tiri pubblici, tiri privati e tiri riservati al solo Master.

**Automazione delle regole.** Il tavolo comprende le regole del sistema attivo fino alla profondità implementata dal modulo di sistema: risolvere attacchi, applicare danni e condizioni, tracciare risorse come gli slot incantesimo, e calcolare valori derivati. L'automazione è autoritativa — il server valida ogni azione contro le regole — ma il Master mantiene l'autorità di sovrascrivere per le decisioni arbitrali.

## 6. Comunicazione e media condivisi

La **chat testuale** è integrata, delimitata al tavolo, con integrazione dei tiri di dado e supporto per la distinzione in-character/fuori personaggio.

**Voce e video** sono integrati, per tavolo, implementati come una **mesh WebRTC peer-to-peer** tra i partecipanti (audio sempre attivo, videocamera opzionale). I media non transitano mai sul server della piattaforma, tranne quando la rete di un partecipante blocca la connessione diretta, nel qual caso un relay TURN sul server trasporta lo stream di quel partecipante. **Non è prevista alcuna registrazione.**

**Audio ambientale sincronizzato.** Il Master carica musica e tracce d'ambiente nella libreria della campagna e controlla la riproduzione per l'intero tavolo (play, pausa, avanzamento, volume). La sincronizzazione è basata su comandi: i client recuperano autonomamente il file audio e obbediscono ai comandi di riproduzione del Master, mantenendo tutti i partecipanti allineati.

## 7. Modello dei contenuti

### 7.1 Personaggi

Le schede personaggio sono **strutturate, specifiche per sistema, e calcolate**: la loro forma è definita dal modulo di sistema attivo, i valori derivati (modificatori, tiri salvezza, capacità) vengono calcolati automaticamente, e la scheda partecipa pienamente all'automazione delle regole. Le schede sono **circoscritte alla campagna** — vivono all'interno di una campagna sotto la piena supervisione del Master — ma ogni Giocatore può **esportare il proprio personaggio** in un formato portabile in qualsiasi momento, così che un personaggio non resti mai ostaggio del ciclo di vita di una campagna.

### 7.2 Compendi

I contenuti dei compendi (incantesimi, mostri, oggetti, voci di regolamento) provengono da due fonti. La piattaforma **include l'SRD di D&D 5e**, importato una volta tramite una pipeline di ingestione offline. I Master possono inoltre **caricare i propri contenuti** nelle loro campagne: le voci conformi agli schemi del sistema attivo partecipano all'automazione esattamente come i contenuti inclusi, mentre il materiale libero è supportato come materiale condiviso (handout, vedi sotto) senza automazione.

### 7.3 Materiali condivisi e visibilità

I Master condividono documenti, immagini e materiali con la propria campagna. La visibilità segue un modello di **default a livello di campagna con eccezioni per singolo elemento e per singolo giocatore**: un materiale condiviso è normalmente visibile all'intero roster, ma il Master può restringere qualsiasi elemento specifico a determinati Giocatori (segreti, lettere private, conoscenze di fazione). Come per la nebbia di guerra, gli elementi ristretti vengono filtrati lato server e non raggiungono mai i client non autorizzati.

### 7.4 Tipi di risorse ospitate

La piattaforma ospita **mappe, token, immagini, documenti/handout PDF e file audio**, con quote di archiviazione per singola campagna. I file video non sono supportati.

## 8. Modello dei sistemi di gioco

Un sistema di gioco è un **artefatto versionato della piattaforma**, sviluppato e distribuito dal mantenitore della piattaforma — mai creato dagli utenti. Ogni modulo di sistema incapsula tre elementi:

1. **Schemi dichiarativi** (JSON) che definiscono la forma delle schede personaggio e delle voci di compendio per quel sistema;
2. **Codice di automazione** che implementa le regole del sistema come gestori delle azioni di giocatori e Master, oltre a calcolatori dei valori derivati e semantica dei dadi;
3. **Un identificativo di versione**, con logica di migrazione esplicita tra le versioni.

Ogni campagna **fissa la versione del sistema** con cui è stata creata. Aggiornare una campagna a una versione più recente del sistema è un'operazione deliberata e migrata — mai implicita — in modo che le campagne di lunga durata e i loro personaggi sopravvivano all'evoluzione dei moduli di sistema. Questa disciplina di versionamento è una regola fondativa del progetto.

Il primo sistema distribuito è **D&D 5e (basato su SRD)**. Il nucleo della piattaforma non contiene alcuna conoscenza delle regole di un gioco specifico.

## 9. Politica sulle licenze dei contenuti

La piattaforma include e ridistribuisce **solo** contenuti per cui ha pieno diritto: per D&D 5e, il System Reference Document rilasciato con licenza Creative Commons. Il materiale dei manuali coperto da copyright (es. contenuto completo del Player's Handbook, mostri non-SRD) **non viene mai distribuito, incluso o ridistribuito dalla piattaforma**, indipendentemente dal fatto che la piattaforma sia privata. I Master che caricano materiale nelle proprie campagne private lo fanno sotto la propria responsabilità; l'obbligo della piattaforma è mantenere tali caricamenti confinati alla campagna a cui appartengono.

## 10. Requisiti non funzionali

**Scala.** Meno di 50 utenti totali; una manciata di tavoli dal vivo concorrenti da 4–6 partecipanti ciascuno. Tutte le decisioni progettuali sono dimensionate su questa realtà, non su un'ipotetica crescita.

**Dispositivi.** Le superfici *di contorno al gioco* (pagine campagna, schede personaggio, materiali condivisi, chat, pianificazione) sono responsive fino al tablet. Il **tavolo dal vivo richiede tablet o superiore per i Giocatori e desktop per il Master**. Gli smartphone non sono esplicitamente supportati per il tavolo dal vivo.

**Lingue.** L'interfaccia è disponibile in **italiano e inglese**, con l'infrastruttura di internazionalizzazione predisposta fin dalla prima release. I contenuti SRD inclusi restano in inglese.

**Sicurezza.** Server-autoritativo ovunque: i client esprimono intenzioni, il server valida e decide. Il filtraggio della visibilità (nebbia di guerra, preparazione nascosta, segreti per singolo giocatore) è imposto al confine del server — nessun dato che un utente non ha diritto di vedere raggiunge mai il suo client. L'autenticazione utilizza nome utente/password con sessioni lato server; i token di invito sono monouso; i codici di recupero dell'amministratore sono monouso.

**Sicurezza dei dati e disponibilità.** Il livello operativo è deliberatamente modesto: disponibilità best-effort senza SLA formale. Il requisito che conta davvero è dichiarato esplicitamente: **la piattaforma deve essere stabile durante le sessioni di gioco pianificate, e una campagna che accumula anni di gioco non deve andare persa.** Di conseguenza, nonostante l'approccio best-effort, viene adottato un dump automatico notturno del database e una sincronizzazione delle risorse verso uno storage esterno (raccomandazione del mantenitore, accettata) come misura standard, e il log degli eventi del motore del tavolo fornisce il ripristino in caso di crash durante le sessioni dal vivo.

**Pubblicità, tracciamento, servizi esterni.** Nessuno. La piattaforma è autocontenuta a eccezione della destinazione di backup esterna e del fallback TURN per WebRTC.

## 11. Vincoli

La piattaforma è costruita e mantenuta da un **singolo sviluppatore**, con competenza in C#, TypeScript/JavaScript, SQL e Python. L'infrastruttura di produzione è un **singolo VPS da 4 GB (Hostinger, ~10 €/mese)**; l'unico carico di lavoro escluso da esso per progettazione è il media dal vivo, che fluisce peer-to-peer. I costi mensili di gestione devono rimanere in questo ordine di grandezza.

## 12. Strategia di rilascio

Il rilascio segue **rollout interni graduali**: il gruppo di gioco del mantenitore stesso gioca sulla piattaforma fin dal primo traguardo utilizzabile, fungendo da tester; il "rilascio ufficiale" alla comunità più ampia avviene quando la visione completa è realizzata. Il gioco reale tra una fase e l'altra è un input progettuale deliberato, specialmente per l'automazione delle regole.

- **Fase 1 — prima giocabilità.** Account, approvazione amministratore, campagne e roster, schede personaggio 5e con valori derivati, chat e dadi, mappe statiche con token e livelli nascosti dal Master. Voce tramite strumenti esterni (Discord) temporaneamente.
- **Fase 2 — il vero tavolo.** Nebbia di guerra, tracciatore di iniziativa, la pipeline intenzione/validazione con la prima automazione 5e (attacchi, danni, condizioni), materiali condivisi con segreti per singolo giocatore, pianificazione delle sessioni con RSVP.
- **Fase 3 — visione completa.** Voce/video integrati (mesh P2P), audio ambientale sincronizzato, automazione 5e più profonda, esportazione dei personaggi, completamento e rifinitura dell'internazionalizzazione. Il rilascio ufficiale chiude questa fase.

## 13. Decisioni architetturali (riepilogo)

Registrate qui per tracciabilità; il documento di architettura è quello autorevole per i dettagli.

| Area | Decisione |
|---|---|
| Backend | Monolite modulare ASP.NET Core (C#); SignalR per la sincronizzazione in tempo reale e la segnalazione WebRTC |
| Frontend | TypeScript + React; PixiJS (WebGL) per il canvas della mappa di battaglia |
| Database | PostgreSQL — nucleo relazionale più JSONB per i contenuti definiti dal sistema |
| Modello real-time | Stato del tavolo server-autoritativo in memoria; intenzione → validazione → broadcast; log degli eventi + snapshot per persistenza e ripristino da crash |
| Sistemi di gioco | Packaging ibrido: schemi JSON + moduli di regole in C# dietro un'interfaccia di piattaforma; le campagne fissano le versioni di sistema |
| Media | Mesh WebRTC P2P; coturn sul VPS come fallback STUN/TURN; sincronizzazione audio basata su comandi |
| Deployment | Docker Compose sul VPS (app, PostgreSQL, coturn, Caddy per TLS); CI su GitHub Actions; dump notturni e sincronizzazione risorse verso storage a oggetti (Cloudflare R2 / Backblaze B2) |
| Strumenti | Python per pipeline offline, in particolare l'ingestione dell'SRD |

## 14. Punti aperti e rischi accettati

- **Rischio di fattibilità (accettato, mitigato):** l'ambito da VTT completo è molto vasto per uno sviluppatore singolo; la strategia di rollout graduale con gioco reale tra le fasi è la mitigazione adottata.
- **Banda TURN:** il traffico di relay attraverso il VPS quando il P2P fallisce si somma al limite di banda di Hostinger; da verificare rispetto ai limiti del piano.
- **Crescita dello storage:** i file audio e le mappe domineranno l'utilizzo del disco; le quote per singola campagna sono richieste fin dalla Fase 1, le cifre esatte sono da definire.
- **Migrazioni delle versioni di sistema:** lo strumento di migrazione tra versioni del modulo 5e è obbligatorio prima di qualsiasi modifica di schema non retrocompatibile — nessuna eccezione.
- **Progettazione dettagliata dei moduli:** il motore del tavolo e il contratto di sistema sono i prossimi obiettivi di progettazione; l'ERD e la superficie API seguiranno dalle loro interfacce.
