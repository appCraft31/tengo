// Palette des bulles 1 → 9 (calculée : distinctes aussi en deutéranopie,
// protanopie et tritanopie ; contraste du chiffre ≥ 4,5:1).
const PAL = ['#FACFCC','#DB9661','#D7C06C','#C6E99B','#65BD90','#7FEFFB','#97C6F7','#A99BE6','#F0A5D7'];

// Icônes au trait (24 × 24, trait 2,1, bouts ronds) : une seule famille.
const ICONS = {
  home: '<path d="M4 11 12 4l8 7v8a1 1 0 0 1-1 1h-4v-6H9v6H5a1 1 0 0 1-1-1z"/>',
  pause: '<path d="M9 6v12M15 6v12"/>',
  bulb: '<path d="M9 18h6M10 21h4M12 3a6 6 0 0 0-3.5 10.9c.6.5 1 1.2 1 2.1h5c0-.9.4-1.6 1-2.1A6 6 0 0 0 12 3z"/>',
  shuffle: '<path d="M4 7h3c4 0 6 10 10 10h3M17 14l3 3-3 3M4 17h3c1.6 0 2.8-1.6 3.8-3.6M13.2 9.6C14.2 8 15.4 7 17 7h3M17 4l3 3-3 3"/>',
  hammer: '<path d="M14 6l4 4M12.5 7.5 5 15a2.1 2.1 0 0 0 3 3l7.5-7.5M11 4h5l3 3v2l-2 1-5-5z"/>',
  restart: '<path d="M4 12a8 8 0 1 0 2.3-5.6M4 4v4h4"/>',
  play: '<path d="M8 5.5v13l10-6.5z"/>',
  gear: '<path d="M10.3 3.5h3.4l.5 2.3 1.6.9 2.2-.8 1.7 2.9-1.7 1.6v1.8l1.7 1.6-1.7 2.9-2.2-.8-1.6.9-.5 2.3h-3.4l-.5-2.3-1.6-.9-2.2.8-1.7-2.9 1.7-1.6v-1.8L4.6 8.8l1.7-2.9 2.2.8 1.6-.9z"/><circle cx="12" cy="12" r="2.8"/>',
  profile: '<circle cx="12" cy="8" r="4"/><path d="M4 21c1.5-4 4.5-6 8-6s6.5 2 8 6"/>',
  duel: '<circle cx="8" cy="8" r="3.2"/><circle cx="16.5" cy="9" r="2.6"/><path d="M2.5 20c.8-3.6 3-5.5 5.5-5.5s4.7 1.9 5.5 5.5M14 14.6c.8-.4 1.6-.6 2.5-.6 2.2 0 4 1.6 4.8 4.8"/>',
  shop: '<path d="M5 8h14l-1 12H6zM9 8V6a3 3 0 0 1 6 0v2"/>',
  bolt: '<path d="M13 3 5 13h6l-1 8 8-10h-6z"/>',
  puzzle: '<path d="M10 4a2 2 0 1 1 4 0v2h4v4h-2a2 2 0 1 0 0 4h2v4h-4v-2a2 2 0 1 0-4 0v2H6v-4h2a2 2 0 1 0 0-4H6V6h4z"/>',
  calendar: '<rect x="4" y="5" width="16" height="15" rx="2.5"/><path d="M8 3v4M16 3v4M4 10h16"/>',
  flame: '<path d="M12 21c4 0 7-2.8 7-6.8 0-3.2-2.2-5.4-3.6-7.2-.3 1.8-1.2 3-2.4 3.6C13.4 7.6 12 5 9.6 3c.2 3-1.6 5-3 7-1 1.3-1.6 2.7-1.6 4.2C5 18.2 8 21 12 21z"/>',
  chevron: '<path d="M9 5l7 7-7 7"/>',
  back: '<path d="M15 5l-7 7 7 7"/>',
  close: '<path d="M6 6l12 12M18 6 6 18"/>',
  star: '<path d="M12 3.5l2.6 5.3 5.9.9-4.3 4.1 1 5.8L12 16.9l-5.2 2.7 1-5.8L3.5 9.7l5.9-.9z"/>',
  brain: '<path d="M9 4a3 3 0 0 0-3 3 3 3 0 0 0-2 5 3 3 0 0 0 2 5 3 3 0 0 0 3 3h0a2 2 0 0 0 2-2V6a2 2 0 0 0-2-2zM15 4a3 3 0 0 1 3 3 3 3 0 0 1 2 5 3 3 0 0 1-2 5 3 3 0 0 1-3 3h0a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2z"/>',
  trophy: '<path d="M8 4h8v5a4 4 0 0 1-8 0zM8 6H4.5a3 3 0 0 0 3.5 4M16 6h3.5a3 3 0 0 1-3.5 4M12 13v4M8.5 20h7M9.5 17h5"/>',
  video: '<rect x="3" y="6" width="13" height="12" rx="2.5"/><path d="M16 10.5 21 8v8l-5-2.5z"/>',
  lock: '<rect x="5" y="11" width="14" height="9" rx="2.5"/><path d="M8 11V8a4 4 0 0 1 8 0v3"/>',
  share: '<path d="M12 3v12M7.5 7.5 12 3l4.5 4.5M5 13v6a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2v-6"/>',
  check: '<path d="M5 12.5l4.5 4.5L19 7.5"/>',
  seed: '<path d="M12 21v-8M12 13c0-4 3-7 7-7 0 4-3 7-7 7zM12 15c0-3-2.4-5.5-6-5.5 0 3 2.4 5.5 6 5.5z"/>',
  noads: '<rect x="3.5" y="6" width="17" height="12" rx="2.5"/><path d="M4 20 20 4"/>',
};
const icon = (k, color = '#34312C', fill = false) =>
  `<svg viewBox="0 0 24 24" fill="${fill ? color : 'none'}" stroke="${color}" stroke-width="2.1" stroke-linecap="round" stroke-linejoin="round">${ICONS[k]}</svg>`;

// Pièce : disque or avec un filet plus foncé, sans reflet.
const coin = (size = 18) => `<span style="display:inline-block;width:${size}px;height:${size}px;border-radius:50%;background:var(--gold);box-shadow:inset 0 0 0 2px #C9962A;vertical-align:-3px"></span>`;
