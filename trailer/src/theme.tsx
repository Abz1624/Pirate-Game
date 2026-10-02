import { staticFile } from "remotion";

// Fonts from the roguepirates.fun site (OFL licences in public/fonts).
const faces = `
@font-face { font-family: "Cinzel"; src: url("${staticFile("fonts/cinzel-latin.woff2")}") format("woff2"); font-weight: 400 900; }
@font-face { font-family: "Alegreya"; src: url("${staticFile("fonts/alegreya-latin.woff2")}") format("woff2"); font-weight: 400 900; }
`;

export const FontFaces = () => <style>{faces}</style>;

export const FPS = 60;
export const W = 1920;
export const H = 1080;

export const C = {
  ink: "#eef4ea",
  gold: "#ffc53d",
  goldHi: "#ffe07a",
  brown: "#5b2a07",
  night: "#071820",
  sea: "#0b4a57",
};

export const serif = '"Cinzel", "Trajan Pro", "Times New Roman", serif';
export const body = '"Alegreya", Georgia, serif';
