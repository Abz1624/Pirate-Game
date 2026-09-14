import assert from 'node:assert/strict';
import { balance, cohorts, results, chestSpend } from './game-economy-simulation.mjs';

const close = (actual, expected, label) =>
  assert.ok(Math.abs(actual - expected) < 1e-6, `${label}: ${actual} != ${expected}`);

assert.equal(chestSpend(12), 776, 'first 12 run chests');

const totalUpgradeGold = Object.values(balance.trackCosts)
  .flat()
  .reduce((sum, cost) => sum + cost, 0);
assert.equal(totalUpgradeGold, 1_927_600, 'total upgrade Gold');

const totalQueueHours = balance.trackHours.reduce((sum, hours) => sum + hours, 0) * 5;
close(totalQueueHours / 24, 70.53819444444444, 'minimum serial queue days');

for (const result of results) {
  const t = result.total;
  const goldSinks = t.upgradeGold + t.cosmeticGold + t.prestigeGold + t.raidGold + t.overflowGold;
  const rumSinks = t.finiteRum + t.reforgeRum + t.contractRum + t.cosmeticRum + t.overflowRum;

  close(500 + result.goldSources - goldSinks, result.ending.gold, `${result.profile} Gold flow`);
  close(180 + result.rumSources - rumSinks, result.ending.rum, `${result.profile} Rum flow`);
  close(balance.finiteRumSinks - t.finiteRum, result.ending.finiteRumRemaining, `${result.profile} finite Rum`);

  assert.ok(result.adShareGold < 0.05, `${result.profile} Gold ads exceed 5%`);
  assert.ok(result.adShareRum < 0.05, `${result.profile} Rum ads exceed 5%`);
  assert.ok(result.overflowShareGold < 0.10, `${result.profile} Gold overflow exceeds 10%`);
  assert.ok(result.overflowShareRum < 0.10, `${result.profile} Rum overflow exceeds 10%`);
  assert.ok(result.completedUpgrades <= 40, `${result.profile} completed too many upgrades`);
  assert.ok(result.ending.gold >= 0 && result.ending.rum >= 0, `${result.profile} negative balance`);
  assert.ok(cohorts[result.profile], `unknown cohort ${result.profile}`);
}

console.log(`PASS: ${results.length} cohorts reconcile; chest curve, progression totals, ad shares, overflow, and balances are within planning guardrails.`);
