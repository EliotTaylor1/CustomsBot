import { useRef, useState } from 'react';

/**
 * Client-side champ-select music (plan §5c). Drop a free-use, non-commercial track at
 * `frontend/public/music/champ-select.mp3`; playback stays entirely in the browser.
 */
export function MusicToggle() {
  const audioRef = useRef<HTMLAudioElement | null>(null);
  const [playing, setPlaying] = useState(false);

  const toggle = () => {
    const audio = audioRef.current;
    if (!audio) return;
    if (playing) {
      audio.pause();
      setPlaying(false);
    } else {
      audio.play().then(() => setPlaying(true)).catch(() => setPlaying(false));
    }
  };

  return (
    <>
      <audio ref={audioRef} src="/music/champ-select.mp3" loop />
      <button className="music" onClick={toggle} title="Champ-select music">{playing ? '♪ on' : '♪ off'}</button>
    </>
  );
}
