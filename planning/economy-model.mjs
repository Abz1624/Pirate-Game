// Planning calculator only. No game code, services or payments are changed.
export const sources = [
 ['Railway rates / Pro minimum','https://railway.com/pricing','Verified 2026-09-14: CPU $0.00000772/vCPU-s; RAM $0.00000386/GB-s; disk $0.00000006/GB-s; service egress $0.05/GB; Pro $20 minimum, inclusive.'],
 ['R2 Standard','https://developers.cloudflare.com/r2/pricing/','Verified: $0.015/GB-month above 10GB, $4.50/million A above 1m, $0.36/million B above 10m; round billable units up; free internet egress.'],
 ['Apple small-business eligibility','https://developer.apple.com/app-store/small-business-program/','15% if enrolled and eligible; do not assume universal eligibility. Model defaults 30% conservatively.'],
 ['Google Play fees','https://support.google.com/googleplay/android-developer/answer/112622','Current schedule differs by market/program/install cohort. Default 30% conservative scenario; 15% sensitivity is not a universal rate.'],
 ['Steam advertising','https://partner.steamgames.com/doc/marketing/advertising?language=english','No ad monetization in Steam build. Store share modeled at 30%; confirm executed distribution agreement.'],
 ['Poki distribution and revenue','https://developers.poki.com/guide/revenue-deal-types','Specific deal guide: web-exclusive revenue share; non-exclusive may be one-time license fee without revenue share. Baseline recurring revenue zero; no unsigned license fee counted. General 50/50 description is not assumed compatible with other web channels.'],
 ['Poki monetization','https://developers.poki.com/guide/how-monetization-works','SDK ads, no IAP. Recurring share requires compatible agreement.'],
 ['CrazyGames monetization','https://docs.crazygames.com/faq/','Ads and optional IAP documented, exact contractual share not verified. Model 50% developer share is a placeholder, NOT a quoted rate.'],
 ['Stripe US domestic cards','https://stripe.com/pricing','2.9% + $0.30 illustrative US domestic-card reference only. Other regions, FX, international cards and merchant-of-record services differ.'],
 ['Rewarded-ad restrictions','https://support.google.com/admob/answer/7313578?hl=en-GB','Rewards must be non-transferable and cannot convert indirectly into transferable items. The model permits small Gold/Rum caches only while both currencies and everything they buy remain account-bound; revalidate the selected ad network before launch.'],
 ['Unity 2026 pricing','https://unity.com/products/pricing-updates','Pro $2,310/seat/year prepaid; Personal threshold up to $200,000 revenue/funding subject to terms. No Runtime Fee. Budgeted seats are editable.'],
 ['Apple enrollment','https://developer.apple.com/programs/enroll/','$99/year US reference; local prices vary.'],
 ['Google enrollment','https://support.google.com/googleplay/android-developer/answer/6112435','$25 one-time enrollment reference.'],
 ['Steam Direct','https://partner.steamgames.com/doc/gettingstarted/appfee','$100/app; recoupable after $1,000 adjusted gross revenue, subject to terms. One-time cash outlay, not recurring hosting.']
];
export const defaults = {
 mau:10000, scenario:'Base', ads:1, payerMultiplier:1, ecpmMultiplier:1,
 verifyRatio:.05, duplicateWork:1.10, headroom:1.5, peakFactor:4,
 batchSeconds:2, checkpointSeconds:10, blobSeconds:30, replayBytesSecond:120,
 replayDays:30, accountMultiple:10, ledgerMonths:12, responseKB:1,
 apiCPUms:2, dbCPUms:2, safety:.25,
 mobileFee:.30, refundRate:.03, indirectTax:.10, directFee:.029, directFixed:.30,
 proSeats:2, ci:60, observability:25, email:10, otherTech:20, techPerMAU:.001,
 payroll:3500, content:1000, support:500, marketing:0, studioPrizes:0,
 sponsorCount:0, sponsorFee:500, sponsorDeliveryCost:150,
 monthlyRetention:.60, developmentRecovery:25000, cashReserve:25000,
 channels:[
  {name:'Direct web',share:.10,stickiness:.20,runs:1.5,minutes:18,payer:.02,arppu:12,orders:1.5,fee:.029,adOpp:.35,fill:.75,ecpm:12,adShare:1},
  {name:'Android',share:.30,stickiness:.20,runs:1.5,minutes:18,payer:.015,arppu:10,orders:1.5,fee:.30,adOpp:.35,fill:.75,ecpm:12,adShare:1},
  {name:'iOS',share:.20,stickiness:.20,runs:1.5,minutes:18,payer:.02,arppu:12,orders:1.5,fee:.30,adOpp:.35,fill:.75,ecpm:12,adShare:1},
  {name:'Steam',share:.10,stickiness:.25,runs:1.5,minutes:22,payer:.025,arppu:12,orders:1.5,fee:.30,adOpp:0,fill:0,ecpm:0,adShare:0},
  {name:'CrazyGames',share:.15,stickiness:.10,runs:1.2,minutes:12,payer:0,arppu:0,orders:0,fee:0,adOpp:1.25,fill:.75,ecpm:7.7,adShare:.50},
  {name:'Poki',share:.15,stickiness:.10,runs:1.2,minutes:12,payer:0,arppu:0,orders:0,fee:0,adOpp:1.25,fill:.75,ecpm:7.7,adShare:0}
 ]
};
export const cases = {
 Downside:{payer:.5,spend:.75,ad:.5,verify:.20},
 Base:{payer:1,spend:1,ad:1,verify:.05},
 Upside:{payer:2,spend:1.25,ad:1.75,verify:.02}
};
export function validate(a) {
 const errors=[];
 for(const [k,v] of Object.entries(a))if(typeof v==='number' && (!Number.isFinite(v)||v<0))errors.push(k+' must be finite and nonnegative');
 for(const k of ['batchSeconds','checkpointSeconds','blobSeconds'])if(a[k]<=0)errors.push(k+' must be positive');
 for(const k of ['refundRate','mobileFee','directFee'])if(a[k]>1)errors.push(k+' must be at most 1');
 if(a.monthlyRetention>=1)errors.push('Monthly retention must be below 100% for the geometric LTV illustration');
 if(Math.abs(a.channels.reduce((s,p)=>s+p.share,0)-1)>1e-6)errors.push('Platform shares must total 100%; users are allocated once');
 for(const p of a.channels)for(const [k,v] of Object.entries(p))if(typeof v==='number' && (!Number.isFinite(v)||v<0||(['share','stickiness','payer','fee','fill','adShare'].includes(k)&&v>1)))errors.push(p.name+': invalid '+k);
 return errors;
}
export function calculate(input=defaults) {
 const a={...defaults,...input}; const c=cases[a.scenario] || cases.Base;
 const days=30,seconds=days*86400;
 const rows=a.channels.map((p,i)=>{
  const mau=a.mau*p.share, dau=mau*p.stickiness;
  const runs=dau*days*p.runs, simSeconds=runs*p.minutes*60;
  const payers=mau*Math.min(1,p.payer*c.payer*a.payerMultiplier);
  const billings=payers*p.arppu*c.spend;
  const afterRefund=billings*(1-a.refundRate);
  const salesExTax=afterRefund/(1+a.indirectTax);
  const fee=i===0?a.directFee:((i===1||i===2)?a.mobileFee:p.fee);
  const platformFees=i===0?billings*fee+payers*p.orders*a.directFixed:salesExTax*fee;
  const netIAP=salesExTax-platformFees;
  // Ad opportunities are blended rewarded/results-screen impressions and always zero on Steam.
  const impressions=(a.ads && i!==3)?dau*days*p.adOpp*p.fill:0;
  const grossAd=impressions/1000*p.ecpm*c.ad*a.ecpmMultiplier;
  const netAd=grossAd*p.adShare;
  return {...p,mau,dau,runs,simSeconds,payers,billings,salesExTax,platformFees,netIAP,impressions,grossAd,netAd,net:netIAP+netAd};
 });
 const sum=k=>rows.reduce((s,r)=>s+r[k],0);
 const simSeconds=sum('simSeconds'),runs=sum('runs'),avgCCU=simSeconds/seconds,peakCCU=avgCCU*a.peakFactor;
 const requests=simSeconds/a.batchSeconds+a.mau*100;
 const writes=simSeconds/a.checkpointSeconds+runs*8+a.mau*20;
 const verifyRatio=a.verifyRatio;
 const verificationCPU=simSeconds*verifyRatio*a.duplicateWork;
 const apiCPU=requests*a.apiCPUms/1000, dbCPU=writes*a.dbCPUms/1000;
 const blocks=Math.max(1,Math.ceil(a.mau/50000));
 const idleCPU=seconds*(.1+.1*blocks);
 const cpu=(verificationCPU+apiCPU+dbCPU+idleCPU)*.00000772;
 const requiredPeakCores=(verificationCPU+apiCPU+dbCPU)/seconds*a.peakFactor*a.headroom;
 const apiRAM=.5+.5*Math.max(1,Math.ceil(peakCCU/250));
 const dbRAM=1+blocks,workerRAM=.5+requiredPeakCores*.25;
 const ram=(apiRAM+dbRAM+workerRAM)*seconds*.00000386;
 const dbGB=5+a.mau*a.accountMultiple*8000/1e9+runs*6*200*a.ledgerMonths/1e9;
 const disk=dbGB*seconds*.00000006;
 const replayUploadGB=(simSeconds*a.replayBytesSecond+runs*2000)/1e9;
 const responseGB=requests*a.responseKB/1e6;
 const egress=(responseGB+replayUploadGB)*.05;
 const railwayUsage=cpu+ram+disk+egress,railway=Math.max(20,railwayUsage);
 const r2GB=2+replayUploadGB*a.replayDays/30+dbGB*2;
 const classA=simSeconds/a.blobSeconds+runs*3+60;
 const classB=classA+runs*3+a.mau*50;
 const r2Storage=Math.ceil(Math.max(0,r2GB-10))*.015;
 const r2A=Math.ceil(Math.max(0,classA-1e6)/1e6)*4.5;
 const r2B=Math.ceil(Math.max(0,classB-1e7)/1e6)*.36;
 const r2=r2Storage+r2A+r2B;
 const hosting=(railway+r2)*(1+a.safety);
 const unity=a.proSeats*2310/12;
 const tools=unity+99/12+2+a.ci+a.observability+a.email+a.otherTech+a.mau*a.techPerMAU;
 const tech=hosting+tools;
 const sponsorship=a.sponsorCount*a.sponsorFee;
 const sponsorCost=a.sponsorCount*a.sponsorDeliveryCost;
 const netRevenue=sum('net')+sponsorship;
 const operations=a.payroll+a.content+a.support+a.marketing+a.studioPrizes+sponsorCost;
 const profit=netRevenue-tech-operations;
 return {rows,mau:a.mau,dau:sum('dau'),runs,simSeconds,avgCCU,peakCCU,requests,writes,verificationCPU,requiredPeakCores,apiRAM,dbRAM,workerRAM,dbGB,replayUploadGB,r2GB,classA,classB,cpu,ram,disk,egress,railwayUsage,railway,r2Storage,r2A,r2B,r2,hosting,unity,tools,tech,operations,billings:sum('billings'),platformFees:sum('platformFees'),netIAP:sum('netIAP'),netAd:sum('netAd'),netRevenue,sponsorship,sponsorCost,profit,
  hostSurplus:netRevenue-hosting-sponsorCost,
  techSurplus:netRevenue-tech-sponsorCost,
  margin:netRevenue>0?profit/netRevenue:null,
  developmentPaybackMonths:profit>0?a.developmentRecovery/profit:null,
  runwayMonths:profit<0?a.cashReserve/-profit:null,
  shareTotal:a.channels.reduce((s,p)=>s+p.share,0)};
}
export function threshold(a,kind='profit') {
 // Piecewise monotonic model over this range. Coarse scan then integer search;
 // infrastructure block boundaries can produce small temporary dips.
 const max=10000000;
 if(calculate({...a,mau:0})[kind]>=0)return 0;
 let lo=0,hi=100;
 while(hi<=max && calculate({...a,mau:hi})[kind]<0){lo=hi;hi=Math.ceil(hi*1.12);}
 if(hi>max)return null;
 while(hi-lo>1){const mid=Math.floor((lo+hi)/2);if(calculate({...a,mau:mid})[kind]>=0)hi=mid;else lo=mid;}
 return hi;
}
export function scaleRows(a){return [1000,10000,50000,100000,500000,1000000].map(mau=>calculate({...a,mau}));}
export function economics(a){
 const r=calculate(a),base=calculate({...a,mau:0});
 const contribution=a.mau>0?(r.netRevenue-r.tech-r.sponsorCost-(base.netRevenue-base.tech-base.sponsorCost))/a.mau:0;
 return {hostingBreakEven:threshold(a,'hostSurplus'),techBreakEven:threshold(a,'techSurplus'),operatingBreakEven:threshold(a,'profit'),contributionPerActiveMonth:contribution,
  illustrativeContributionLTV:contribution/(1-a.monthlyRetention)};
}
