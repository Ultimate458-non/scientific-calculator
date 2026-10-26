// Browser side of exam mode. It watches for the student leaving the page and tells the C# code.
// A web page cannot truly stop someone switching apps; it can only notice, make noise and keep a record.
window.examGuard = (function () {
  let ref = null, audio = null, alarmTimer = null, wake = null, active = false, away = false;
  let handlers = null;

  const send = (name, ...args) => { if (ref) ref.invokeMethodAsync(name, ...args).catch(() => {}); };
  const buzz = (p) => { try { if (navigator.vibrate) navigator.vibrate(p); } catch (e) {} };

  function leave(reason) {
    if (!active || away) return;
    away = true;
    buzz([400, 150, 400]);
    send('OnAway', reason);
  }
  function back() {
    if (!active || !away || document.visibilityState !== 'visible') return;
    away = false;
    send('OnBack');
  }

  async function getWake() {
    try { if ('wakeLock' in navigator) wake = await navigator.wakeLock.request('screen'); } catch (e) {}
  }

  function ensureAudio() {
    try {
      if (!audio) audio = new (window.AudioContext || window.webkitAudioContext)();
      if (audio.state === 'suspended') audio.resume();
    } catch (e) {}
  }

  function beep() {
    if (!audio) return;
    try {
      const o = audio.createOscillator(), g = audio.createGain();
      o.type = 'square';
      o.frequency.value = 880;
      g.gain.value = 0.3;
      o.connect(g); g.connect(audio.destination);
      o.frequency.setValueAtTime(620, audio.currentTime + 0.25);
      o.start(); o.stop(audio.currentTime + 0.5);
    } catch (e) {}
  }

  return {
    canVibrate: () => 'vibrate' in navigator,
    canFullscreen: () => !!document.documentElement.requestFullscreen,
    utcOffset: () => -new Date().getTimezoneOffset(),

    start: function (dotnetRef) {
      ref = dotnetRef; active = true; away = false;
      ensureAudio();
      try { document.documentElement.requestFullscreen({ navigationUI: 'hide' }).catch(() => {}); } catch (e) {}
      getWake();
      history.pushState(null, '', location.href);

      handlers = {
        visibilitychange: () => { if (document.visibilityState === 'hidden') leave('switched app or locked the screen'); else { getWake(); back(); } },
        blur: () => leave('left the calculator window'),
        focus: () => setTimeout(back, 60),
        pagehide: () => leave('closed or reloaded the page'),
        fullscreenchange: () => { if (active && !document.fullscreenElement) send('OnFullscreenExit'); },
        beforeunload: (e) => { if (active) { e.preventDefault(); e.returnValue = ''; } },
        popstate: () => { if (active) history.pushState(null, '', location.href); },
        contextmenu: (e) => { if (active) e.preventDefault(); },
      };
      document.addEventListener('visibilitychange', handlers.visibilitychange);
      document.addEventListener('fullscreenchange', handlers.fullscreenchange);
      document.addEventListener('contextmenu', handlers.contextmenu);
      window.addEventListener('blur', handlers.blur);
      window.addEventListener('focus', handlers.focus);
      window.addEventListener('pagehide', handlers.pagehide);
      window.addEventListener('beforeunload', handlers.beforeunload);
      window.addEventListener('popstate', handlers.popstate);
    },

    // Reloaded during an exam: listeners only, the student has to tap to go full screen again.
    resume: function (dotnetRef) { this.start(dotnetRef); },

    goFullscreen: function () {
      ensureAudio();
      try { document.documentElement.requestFullscreen({ navigationUI: 'hide' }).catch(() => {}); } catch (e) {}
      getWake();
    },

    stop: function () {
      active = false; away = false; ref = null;
      this.alarm(false);
      if (handlers) {
        document.removeEventListener('visibilitychange', handlers.visibilitychange);
        document.removeEventListener('fullscreenchange', handlers.fullscreenchange);
        document.removeEventListener('contextmenu', handlers.contextmenu);
        window.removeEventListener('blur', handlers.blur);
        window.removeEventListener('focus', handlers.focus);
        window.removeEventListener('pagehide', handlers.pagehide);
        window.removeEventListener('beforeunload', handlers.beforeunload);
        window.removeEventListener('popstate', handlers.popstate);
        handlers = null;
      }
      try { if (wake) wake.release(); } catch (e) {}
      wake = null;
      try { if (document.fullscreenElement) document.exitFullscreen(); } catch (e) {}
    },

    // Loud, repeating beep and long vibration until switched off.
    alarm: function (on) {
      if (alarmTimer) { clearInterval(alarmTimer); alarmTimer = null; }
      if (on) {
        ensureAudio();
        const tick = () => { beep(); buzz([500, 200, 500]); };
        tick();
        alarmTimer = setInterval(tick, 1600);
      } else {
        buzz(0);
      }
    },

    shortBuzz: function () { buzz([250, 100, 250]); },

    copy: async function (text) {
      try { await navigator.clipboard.writeText(text); return true; } catch (e) { return false; }
    },

    download: function (name, text) {
      const blob = new Blob([text], { type: 'text/plain;charset=utf-8' });
      const a = document.createElement('a');
      a.href = URL.createObjectURL(blob);
      a.download = name;
      document.body.appendChild(a); a.click(); a.remove();
      setTimeout(() => URL.revokeObjectURL(a.href), 2000);
    },
  };
})();
