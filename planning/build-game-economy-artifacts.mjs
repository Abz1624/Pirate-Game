import fs from 'node:fs/promises';
import {balance,cohorts,results} from './game-economy-simulation.mjs';

await fs.writeFile('Rogue_Pirates_Game_Economy_Simulation.json',JSON.stringify({generatedAt:'2026-09-14',status:'planning assumptions, not telemetry',balance,cohorts,results},null,2));
console.log('Wrote Rogue_Pirates_Game_Economy_Simulation.json');
