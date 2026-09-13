// Rogue Pirates design simulation. This is a tunable planning model, not game code.
export const balance = {
  days: 180,
  goldPerKill: 1.65,
  chestBase: 25,
  chestGrowth: 1.16,
  chestCap: 1200,
  adGold: 75,
  adRum: 5,
  adClaimsPerDayCap: 2,
  goldProduction: [10,15,22,32,45,60,80,105,135],
  rumProduction: [.5,.75,1.1,1.6,2.3,3.2,4.5,6.2,8],
  goldCapacity: [2000,4000,8000,16000,32000,75000,150000,300000,500000],
  rumCapacity: [600,900,1400,2200,3400,5200,8000,12000,18000],
  trackCosts: {
    goldProduction:[400,1200,3500,8000,18000,40000,90000,180000],
    rumProduction:[400,1200,3500,8000,18000,40000,90000,180000],
    goldCapacity:[500,1500,4000,9000,20000,45000,100000,200000],
    rumCapacity:[500,1500,4000,9000,20000,45000,100000,200000],
    defense:[600,1800,5000,12000,26000,60000,130000,250000]
  },
  trackHours:[1/12,.5,2,6,18,48,96,168],
  liveOpsPer30:{gold:4800,rum:224},
  finiteRumSinks:11080,
  reforgeRum:20
};

export const cohorts = {
  Casual:{runsPerDay:2,kills:500,pickup:.82,goldBonus:.05,bossWin:.35,objectiveFactor:1,chests:6,claims:1,adClaims:.35,adRumShare:.35,raidLoss:.003,goldCosmeticsPer30:1000,prestigeGoldPer30:0,rumCosmeticsPer30:120,reforgesPerRun:.015,contractRumPerRun:8},
  Active:{runsPerDay:6,kills:1000,pickup:.90,goldBonus:.15,bossWin:.60,objectiveFactor:1.6,chests:9,claims:2,adClaims:.80,adRumShare:.35,raidLoss:.008,goldCosmeticsPer30:5000,prestigeGoldPer30:120000,rumCosmeticsPer30:400,reforgesPerRun:.03,contractRumPerRun:30},
  HighlyEngaged:{runsPerDay:15,kills:1600,pickup:.95,goldBonus:.25,bossWin:.78,objectiveFactor:2.2,chests:12,claims:3,adClaims:1.20,adRumShare:.30,raidLoss:.012,goldCosmeticsPer30:15000,prestigeGoldPer30:900000,rumCosmeticsPer30:1000,reforgesPerRun:.05,contractRumPerRun:60}
};

const sum=a=>a.reduce((x,y)=>x+y,0);
export function chestSpend(count,b=balance){return sum(Array.from({length:count},(_,k)=>Math.ceil(Math.min(b.chestCap,b.chestBase*b.chestGrowth**k))));}
function gateRank(day){return day<14?3:day<45?6:9;}

export function simulate(profileName, overrides={}){
  const b={...balance,...overrides};
  const p={...cohorts[profileName],...(overrides.profile||{})};
  const ranks={goldProduction:1,rumProduction:1,goldCapacity:1,rumCapacity:1,defense:1};
  const order=['goldCapacity','goldProduction','defense','rumCapacity','rumProduction'];
  let queue=null, orderIndex=0, gold=500,rum=180,finiteRumRemaining=b.finiteRumSinks;
  const total={runGoldGross:0,chestGold:0,bossGold:0,passiveGold:0,liveOpsGold:0,adGold:0,upgradeGold:0,cosmeticGold:0,prestigeGold:0,raidGold:0,overflowGold:0,runRum:0,passiveRum:0,liveOpsRum:0,adRum:0,finiteRum:0,reforgeRum:0,contractRum:0,cosmeticRum:0,overflowRum:0};
  let completedUpgrades=0;
  for(let day=0;day<b.days;day++){
    if(queue && queue.done<=day){ranks[queue.track]++;queue=null;completedUpgrades++;}
    const hours=Math.min(24,8*p.claims);
    const passiveGold=hours*b.goldProduction[ranks.goldProduction-1];
    const passiveRum=hours*b.rumProduction[ranks.rumProduction-1];
    const grossPerRun=p.kills*b.goldPerKill*p.pickup*(1+p.goldBonus);
    const chestPerRun=Math.min(grossPerRun,chestSpend(p.chests,b));
    const bossGoldPerRun=p.bossWin*120*p.objectiveFactor;
    const killRumPerRun=Math.min(20*Math.ceil(p.objectiveFactor),Math.floor(p.kills/100));
    const bossRumPerRun=p.bossWin*30*p.objectiveFactor;
    const runGold=p.runsPerDay*(grossPerRun-chestPerRun+bossGoldPerRun);
    const runRum=p.runsPerDay*(killRumPerRun+bossRumPerRun);
    const liveGold=b.liveOpsPer30.gold/30, liveRum=b.liveOpsPer30.rum/30;
    const adClaims=Math.min(b.adClaimsPerDayCap,p.adClaims);
    const adGold=adClaims*(1-p.adRumShare)*b.adGold;
    const adRum=adClaims*p.adRumShare*b.adRum;
    total.runGoldGross+=p.runsPerDay*grossPerRun;total.chestGold+=p.runsPerDay*chestPerRun;total.bossGold+=p.runsPerDay*bossGoldPerRun;total.passiveGold+=passiveGold;total.liveOpsGold+=liveGold;total.adGold+=adGold;
    total.runRum+=runRum;total.passiveRum+=passiveRum;total.liveOpsRum+=liveRum;total.adRum+=adRum;
    gold+=runGold+passiveGold+liveGold+adGold;rum+=runRum+passiveRum+liveRum+adRum;
    const cosmeticGold=Math.min(gold,p.goldCosmeticsPer30/30);gold-=cosmeticGold;total.cosmeticGold+=cosmeticGold;
    const prestigeGold=Math.min(gold,p.prestigeGoldPer30/30);gold-=prestigeGold;total.prestigeGold+=prestigeGold;
    const cosmeticRum=Math.min(rum,p.rumCosmeticsPer30/30);rum-=cosmeticRum;total.cosmeticRum+=cosmeticRum;
    const reforge=Math.min(rum,p.runsPerDay*p.reforgesPerRun*b.reforgeRum);rum-=reforge;total.reforgeRum+=reforge;
    const contracts=Math.min(rum,p.runsPerDay*p.contractRumPerRun);rum-=contracts;total.contractRum+=contracts;
    const finite=Math.min(rum,finiteRumRemaining,Math.max(0,120));rum-=finite;finiteRumRemaining-=finite;total.finiteRum+=finite;
    if(!queue){
      for(let tries=0;tries<order.length;tries++){
        const track=order[(orderIndex+tries)%order.length], rank=ranks[track];
        if(rank<gateRank(day)){
          const cost=b.trackCosts[track][rank-1];
          if(gold>=cost){gold-=cost;total.upgradeGold+=cost;queue={track,done:day+b.trackHours[rank-1]/24};orderIndex=(order.indexOf(track)+1)%order.length;break;}
        }
      }
    }
    const raidLoss=gold*p.raidLoss;gold-=raidLoss;total.raidGold+=raidLoss;
    const goldCap=b.goldCapacity[ranks.goldCapacity-1],rumCap=b.rumCapacity[ranks.rumCapacity-1];
    if(gold>goldCap){total.overflowGold+=gold-goldCap;gold=goldCap;}
    if(rum>rumCap){total.overflowRum+=rum-rumCap;rum=rumCap;}
  }
  const goldSources=total.runGoldGross-total.chestGold+total.bossGold+total.passiveGold+total.liveOpsGold+total.adGold;
  const rumSources=total.runRum+total.passiveRum+total.liveOpsRum+total.adRum;
  return {profile:profileName,days:b.days,ranks,completedUpgrades,ending:{gold,rum,finiteRumRemaining},total,goldSources,rumSources,
    adShareGold:total.adGold/goldSources,adShareRum:total.adRum/rumSources,
    overflowShareGold:total.overflowGold/goldSources,overflowShareRum:total.overflowRum/rumSources,
    per30:{goldSources:goldSources/b.days*30,rumSources:rumSources/b.days*30,bankedRunGold:(total.runGoldGross-total.chestGold+total.bossGold)/b.days*30,adGold:total.adGold/b.days*30,adRum:total.adRum/b.days*30,overflowGold:total.overflowGold/b.days*30,overflowRum:total.overflowRum/b.days*30}};
}

export const results=Object.keys(cohorts).map(simulate);
if(import.meta.url===`file:///${process.argv[1]?.replaceAll('\\','/')}`)console.log(JSON.stringify({balance,cohorts,results},null,2));
