import assert from 'node:assert/strict';
import {defaults, calculate, validate, threshold} from './economy-model.mjs';

const a={...structuredClone(defaults),mau:100000};
const r=calculate(a);
const near=(x,y)=>assert.ok(Math.abs(x-y)<1e-7,`${x} != ${y}`);
near(r.rows.reduce((s,p)=>s+p.mau,0),a.mau);
near(r.rows.reduce((s,p)=>s+p.net,0),r.netRevenue);
near(r.netRevenue-calculate({...a,ads:0}).netRevenue,1296.421875);
near(r.tech,calculate({...a,ads:0}).tech);
assert.ok(calculate({...a,verifyRatio:.2}).tech>r.tech);
near(calculate({...a,verifyRatio:.2}).netRevenue,r.netRevenue);
assert.ok(calculate({...a,mobileFee:.15}).netRevenue>r.netRevenue);
near(calculate({...a,marketing:1000}).profit,r.profit-1000);
const free={...a,payerMultiplier:0,ads:0};
assert.equal(calculate(free).netRevenue,0);
assert.equal(threshold(free),null);
const zero=calculate({...a,mau:0});
assert.equal(zero.netRevenue,0);
assert.ok(zero.tech>0 && Number.isFinite(zero.tech));
for(const kind of ['hostSurplus','techSurplus','profit']){
 const n=threshold(a,kind);
 assert.ok(calculate({...a,mau:n})[kind]>=0);
 assert.ok(calculate({...a,mau:n-1})[kind]<0);
}
assert.deepEqual(validate(a),[]);
assert.ok(validate({...a,monthlyRetention:1}).length);
assert.ok(validate({...a,blobSeconds:0}).length);
assert.ok(validate({...a,mau:-1}).length);
const invalid=structuredClone(a);invalid.channels[0].share=.2;
assert.ok(validate(invalid).length);
console.log('PASS: revenue reconciliation, ads, fees, CPU sensitivity, zero-activity/revenue, break-even boundaries, and invalid-input checks.');
