# Maakt de teksten in src/Lang.cs: per sleutel Nederlands, Engels, Spaans, Frans.
# Pas hier een tekst aan en draai: python build/lang.py
# key: (nl, en, es, fr)
T = [
("act.sleep","Slaapstand","Sleep","Suspender","Veille"),
("act.hibernate","Sluimerstand","Hibernate","Hibernar","Veille prolongée"),
("act.restart","Opnieuw opstarten","Restart","Reiniciar","Redémarrer"),
("act.shutdown","Afsluiten","Shut down","Apagar","Éteindre"),
("act.lock","Vergrendelen","Lock","Bloquear","Verrouiller"),
("act.logoff","Afmelden","Sign out","Cerrar sesión","Se déconnecter"),
("now.sleep","Nu in slaapstand","Sleep now","Suspender ahora","Mettre en veille"),
("now.hibernate","Nu in sluimerstand","Hibernate now","Hibernar ahora","Veille prolongée maintenant"),
("now.restart","Nu opnieuw opstarten","Restart now","Reiniciar ahora","Redémarrer maintenant"),
("now.shutdown","Nu afsluiten","Shut down now","Apagar ahora","Éteindre maintenant"),
("now.lock","Nu vergrendelen","Lock now","Bloquear ahora","Verrouiller maintenant"),
("now.logoff","Nu afmelden","Sign out now","Cerrar sesión ahora","Se déconnecter maintenant"),
("in","over {0}","in {0}","en {0}","dans {0}"),
("at","om {0}","at {0}","a las {0}","à {0}"),
("now","Nu","Now","Ahora","Maintenant"),
("unit.h","uur","h","h","h"),
("unit.s","sec","sec","s","s"),
("hint.time","Typ minuten of een tijd, zoals 23:30","Type minutes or a time, like 23:30","Escribe minutos o una hora, como 23:30","Tapez des minutes ou une heure, comme 23:30"),
("unavailable","Staat uit in Windows","Turned off in Windows","Desactivado en Windows","Désactivé dans Windows"),
("btn.cancel","Annuleren","Cancel","Cancelar","Annuler"),
("btn.donow","Nu doen","Do it now","Hacerlo ya","Maintenant"),
("btn.add","+{0} min","+{0} min","+{0} min","+{0} min"),
("btn.start","Start timer","Start timer","Iniciar","Démarrer"),
("btn.done","Klaar","Done","Listo","OK"),
("warn.save","Sla je werk op.","Save your work.","Guarda tu trabajo.","Enregistrez votre travail."),
("undo.hint","Druk op Esc om te stoppen","Press Esc to stop","Pulsa Esc para detener","Appuyez sur Échap pour arrêter"),
("running.at","om {0}","at {0}","a las {0}","à {0}"),
("info.instant","Na Enter: meteen {0}","After Enter: {0} right away","Tras Enter: {0} al instante","Après Entrée : {0} tout de suite"),
("info.delay","Na Enter: {1} seconden aftellen, dan {0}","After Enter: {1} second countdown, then {0}","Tras Enter: cuenta atrás de {1} segundos y {0}","Après Entrée : {1} secondes, puis {0}"),
("picker.in","Over","In","Dentro de","Dans"),
("picker.at","Om","At","A las","À"),
("picker.h","uur","hours","horas","heures"),
("picker.m","min","min","min","min"),
("picker.preset","Wordt een vaste knop: {0}","Becomes a fixed button: {0}","Será un botón fijo: {0}","Devient un bouton fixe : {0}"),
("menu.remove","Verwijderen","Remove","Quitar","Supprimer"),
("tray.open","Openen","Open","Abrir","Ouvrir"),
("tray.cancel","Timer annuleren","Cancel timer","Cancelar temporizador","Annuler la minuterie"),
("tray.add","+{0} minuten","+{0} minutes","+{0} minutos","+{0} minutes"),
("tray.settings","Instellingen","Settings","Configuración","Paramètres"),
("tray.exit","Dusk stoppen","Quit Dusk","Salir de Dusk","Quitter Dusk"),
("note.resumed","Je timer is gestopt, omdat de pc in slaapstand was.","Your timer was stopped because the PC was asleep.","Tu temporizador se detuvo porque el PC estaba suspendido.","Votre minuterie a été arrêtée car le PC était en veille."),
("note.hotkey","Sneltoets {0} was bezet, Dusk gebruikt nu {1}.","Shortcut {0} was taken, Dusk now uses {1}.","El atajo {0} estaba ocupado, Dusk usa ahora {1}.","Le raccourci {0} était pris, Dusk utilise maintenant {1}."),
("err.sleep","Slaapstand is niet beschikbaar op deze pc.","Sleep is not available on this PC.","La suspensión no está disponible en este PC.","La veille n'est pas disponible sur ce PC."),
("err.hibernate","Sluimerstand staat uit in Windows.","Hibernate is turned off in Windows.","La hibernación está desactivada en Windows.","La veille prolongée est désactivée dans Windows."),
("err.privilege","Windows staat afsluiten niet toe.","Windows does not allow shutting down.","Windows no permite apagar.","Windows n'autorise pas l'arrêt."),
("err.generic","Dat lukte niet. Probeer het nog eens.","That did not work. Please try again.","No ha funcionado. Inténtalo de nuevo.","Cela n'a pas fonctionné. Réessayez."),
("set.title","Instellingen","Settings","Configuración","Paramètres"),
("nav.general","Algemeen","General","General","Général"),
("nav.look","Uiterlijk","Appearance","Apariencia","Apparence"),
("nav.keys","Sneltoets en timer","Shortcut and timer","Atajo y temporizador","Raccourci et minuterie"),
("nav.about","Over","About","Acerca de","À propos"),
("set.lang","Taal","Language","Idioma","Langue"),
("set.autostart","Starten met Windows","Start with Windows","Iniciar con Windows","Démarrer avec Windows"),
("set.autostart.d","Staat klaar in de taakbalk, gebruikt bijna geen geheugen","Waits in the taskbar, uses almost no memory","Espera en la barra de tareas, casi no usa memoria","Attend dans la barre des tâches, presque sans mémoire"),
("set.forced","Geforceerd afsluiten","Force shutdown","Forzar apagado","Forcer l'arrêt"),
("set.forced.d","Niet wachten op programma's die nog willen opslaan","Do not wait for apps that still want to save","No esperar a las aplicaciones que quieren guardar","Ne pas attendre les applications qui veulent enregistrer"),
("set.lock","Vergrendelen en afmelden tonen","Show lock and sign out","Mostrar bloquear y cerrar sesión","Afficher verrouiller et déconnexion"),
("set.lock.d","Twee extra opties in het menu","Two extra options in the menu","Dos opciones más en el menú","Deux options de plus dans le menu"),
("set.default","Standaard gekozen","Selected by default","Seleccionado por defecto","Sélectionné par défaut"),
("set.default.d","Deze optie staat klaar als je Dusk opent","This option is ready when you open Dusk","Esta opción está lista al abrir Dusk","Cette option est prête à l'ouverture de Dusk"),
("set.open","Waar opent Dusk","Where Dusk opens","Dónde se abre Dusk","Où s'ouvre Dusk"),
("set.open.d","Je kunt het venster ook verslepen aan de bovenkant","You can also drag the window by its top","También puedes arrastrar la ventana por arriba","Vous pouvez aussi déplacer la fenêtre par le haut"),
("open.smart","Slim","Smart","Inteligente","Intelligent"),
("open.tray","Bij de taakbalk","By the taskbar","Junto a la barra","Près de la barre"),
("open.center","In het midden","In the middle","En el centro","Au centre"),
("open.last","Waar ik hem neerzet","Where I put it","Donde lo dejé","Là où je la place"),
("open.smart.d","Bij de taakbalk als je op het icoon klikt, anders in het midden","By the taskbar when you click the icon, otherwise in the middle","Junto a la barra al pulsar el icono, si no en el centro","Près de la barre en cliquant l'icône, sinon au centre"),
("set.theme","Licht of donker","Light or dark","Claro u oscuro","Clair ou sombre"),
("theme.system","Systeem","System","Sistema","Système"),
("theme.light","Licht","Light","Claro","Clair"),
("theme.dark","Donker","Dark","Oscuro","Sombre"),
("theme.system.d","volgt Windows","follows Windows","sigue a Windows","suit Windows"),
("set.backdrop","Achtergrond","Background","Fondo","Arrière-plan"),
("bd.mica","Mica","Mica","Mica","Mica"),
("bd.mica.d","kleur van je bureaublad","colour of your desktop","color de tu escritorio","couleur de votre bureau"),
("bd.solid","Effen","Solid","Liso","Uni"),
("bd.solid.d","vast grijs","plain grey","gris fijo","gris fixe"),
("bd.dusk","Schemer","Dusk","Atardecer","Crépuscule"),
("bd.dusk.d","nacht naar gloed","night to glow","de noche a brillo","de la nuit à la lueur"),
("set.accent","Accentkleur","Accent colour","Color de acento","Couleur d'accent"),
("acc.lila","Lila","Lilac","Lila","Lilas"),
("acc.perzik","Perzik","Peach","Melocotón","Pêche"),
("acc.roze","Roze","Pink","Rosa","Rose"),
("acc.blauw","Blauw","Blue","Azul","Bleu"),
("acc.windows","Windows","Windows","Windows","Windows"),
("set.hotkey","Sneltoets om te openen","Shortcut to open","Atajo para abrir","Raccourci pour ouvrir"),
("set.hotkey.d","Werkt overal, ook in een ander programma","Works everywhere, even in other apps","Funciona en todas partes","Fonctionne partout"),
("set.change","Wijzigen","Change","Cambiar","Modifier"),
("set.press","Druk nu je sneltoets in...","Press your shortcut now...","Pulsa ahora tu atajo...","Appuyez sur votre raccourci..."),
("set.taken","Die sneltoets is al bezet. Kies een andere.","That shortcut is taken. Choose another one.","Ese atajo está ocupado. Elige otro.","Ce raccourci est déjà pris. Choisissez-en un autre."),
("set.step","Stap voor + en −","Step for + and −","Paso para + y −","Pas pour + et −"),
("set.step.d","Hoeveel tijd er per druk bij of af gaat","How much time each press adds or removes","Cuánto tiempo suma o resta cada pulsación","Le temps ajouté ou retiré à chaque appui"),
("set.warn","Waarschuwing vooraf","Warning beforehand","Aviso previo","Avertissement"),
("set.warn.d","Een melding voordat de timer afloopt","A notice before the timer runs out","Un aviso antes de que acabe el temporizador","Un avis avant la fin de la minuterie"),
("warn.off","Uit","Off","No","Non"),
("set.delay","Aftellen bij Nu","Countdown for Now","Cuenta atrás con Ahora","Compte à rebours avec Maintenant"),
("set.delay.d","Zo kun je op het laatste moment nog stoppen","So you can still stop at the last moment","Para poder parar en el último momento","Pour pouvoir encore arrêter au dernier moment"),
("delay.off","Meteen","Right away","Al instante","Tout de suite"),
("set.presets","Vaste tijden","Fixed times","Tiempos fijos","Durées fixes"),
("set.presets.d","Met + toevoegen, rechtsklik om te verwijderen","Add with +, right-click to remove","Añade con +, clic derecho para quitar","Ajoutez avec +, clic droit pour supprimer"),
("btn.reset","Herstellen","Reset","Restablecer","Réinitialiser"),
("on","Aan","On","Sí","Oui"),
("off","Uit","Off","No","Non"),
("about.version","Versie {0}","Version {0}","Versión {0}","Version {0}"),
("about.text","Dusk zet je pc uit, in slaap of opnieuw aan. Met een sneltoets en je pijltjestoetsen, en een timer als je wilt.","Dusk shuts down, sleeps or restarts your PC. With a shortcut and your arrow keys, and a timer if you like.","Dusk apaga, suspende o reinicia tu PC. Con un atajo y las flechas, y un temporizador si quieres.","Dusk éteint, met en veille ou redémarre votre PC. Avec un raccourci et les flèches, et une minuterie si vous voulez."),
("about.log","Logboek openen","Open log","Abrir registro","Ouvrir le journal"),
("about.github","Bekijken op GitHub","View on GitHub","Ver en GitHub","Voir sur GitHub"),
("about.privacy","Werkt helemaal offline. Er gaat niets naar internet.","Works fully offline. Nothing is sent to the internet.","Funciona sin conexión. No se envía nada a internet.","Fonctionne hors ligne. Rien n'est envoyé sur internet."),
("nav.update","Bijwerken","Update","Actualizar","Mise à jour"),
("upd.ready","Versie {0} staat klaar","Version {0} is ready","La versión {0} está lista","La version {0} est prête"),
("upd.ready.d","Dusk sluit even en start daarna vanzelf weer","Dusk closes briefly and then starts again","Dusk se cierra un momento y vuelve a abrirse","Dusk se ferme un instant puis redémarre"),
("upd.checking","Even kijken...","Checking...","Comprobando...","Vérification..."),
("upd.checking.d","Dusk vraagt GitHub of er een nieuwe versie is","Dusk asks GitHub if there is a new version","Dusk pregunta a GitHub si hay una versión nueva","Dusk demande à GitHub s'il y a une nouvelle version"),
("upd.latest","Je hebt de nieuwste versie","You have the latest version","Tienes la última versión","Vous avez la dernière version"),
("upd.latest.d","Dusk {0}, gekeken om {1}","Dusk {0}, checked at {1}","Dusk {0}, comprobado a las {1}","Dusk {0}, vérifié à {1}"),
("upd.failed","Kijken lukte niet","Could not check","No se pudo comprobar","Vérification impossible"),
("upd.failed.d","Geen internet? Probeer het straks nog eens.","No internet? Try again later.","¿Sin internet? Inténtalo más tarde.","Pas d'internet ? Réessayez plus tard."),
("upd.idle.d","Kijk of er een nieuwe versie is","Check if there is a new version","Comprueba si hay una versión nueva","Vérifiez s'il y a une nouvelle version"),
("upd.check","Nu kijken","Check now","Comprobar","Vérifier"),
("upd.install","Nu bijwerken","Update now","Actualizar ya","Mettre à jour"),
("upd.busy","Bezig...","Working...","Trabajando...","En cours..."),
("upd.notify","Melding als er een nieuwe versie is","Notify me about new versions","Avisarme de versiones nuevas","M'avertir des nouvelles versions"),
("upd.notify.d","Alleen een vraag aan GitHub, er gaat niets van jou mee","Only a question to GitHub, nothing of yours is sent","Solo una consulta a GitHub, no se envía nada tuyo","Juste une question à GitHub, rien de vous n'est envoyé"),
("upd.news","Wat is er nieuw","What is new","Novedades","Nouveautés"),
("upd.yours","jouw versie","your version","tu versión","votre version"),
("upd.confirm","Dusk bijwerken naar {0}? Dusk sluit even en start daarna vanzelf weer.","Update Dusk to {0}? Dusk closes briefly and then starts again.","¿Actualizar Dusk a {0}? Dusk se cierra un momento y vuelve a abrirse.","Mettre à jour Dusk vers {0} ? Dusk se ferme un instant puis redémarre."),
("upd.confirm.timer","Je timer stopt dan.","Your timer will stop.","Tu temporizador se detendrá.","Votre minuterie s'arrêtera."),
("upd.found","Er is een nieuwe versie: {0}. Klik hier om bij te werken.","There is a new version: {0}. Click here to update.","Hay una versión nueva: {0}. Haz clic para actualizar.","Une nouvelle version est disponible : {0}. Cliquez pour mettre à jour."),
("upd.done","Dusk is bijgewerkt naar {0}.","Dusk was updated to {0}.","Dusk se ha actualizado a {0}.","Dusk a été mis à jour vers {0}."),
("upd.fail","Bijwerken lukte niet. Probeer het later nog eens.","Updating did not work. Please try again later.","No se pudo actualizar. Inténtalo más tarde.","La mise à jour a échoué. Réessayez plus tard."),
("upd.downloading","De nieuwe versie wordt opgehaald...","Downloading the new version...","Descargando la versión nueva...","Téléchargement de la nouvelle version..."),
("quit.timer","Dusk stoppen? Je timer stopt dan.","Quit Dusk? Your timer will stop.","¿Salir de Dusk? Tu temporizador se detendrá.","Quitter Dusk ? Votre minuterie s'arrêtera."),
("tray.update","Bijwerken naar {0}","Update to {0}","Actualizar a {0}","Mettre à jour vers {0}"),
]
def q(s): return '"' + s.replace('\\','\\\\').replace('"','\\"') + '"'
keys = ",\n            ".join(q(t[0]) for t in T)
def vals(i): return ",\n                ".join(q(t[i]) for t in T)
import os
LANG = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'src', 'Lang.cs')
src = open(LANG, encoding='utf-8').read()
start = src.index('        static readonly string[] Keys = {')
end = src.index('        public static string ActionName(PowerAction a)')
block = '''        static readonly string[] Keys = {
            %s
        };

        static void Add(string lang, params string[] values)
        {
            if (values.Length != Keys.Length) throw new InvalidOperationException("Taal " + lang + ": " + values.Length + " teksten, verwacht " + Keys.Length);
            Dictionary<string, string> d = new Dictionary<string, string>();
            for (int i = 0; i < Keys.Length; i++) d[Keys[i]] = values[i];
            All[lang] = d;
        }

        public static int KeyCount { get { return Keys.Length; } }

        // Gemaakt met build/lang.py: per sleutel de vier talen naast elkaar, zodat ze nooit verschuiven
        static Lang()
        {
            Add("nl",
                %s);
            Add("en",
                %s);
            Add("es",
                %s);
            Add("fr",
                %s);
        }

''' % (keys, vals(1), vals(2), vals(3), vals(4))
open(LANG,'w',encoding='utf-8').write(src[:start] + block + src[end:])
print(len(T))
