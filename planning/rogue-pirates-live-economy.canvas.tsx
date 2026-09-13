import {H1,H2,Text,Row,Stat,Table,LineChart,useHostTheme,useState} from 'cursor/canvas';

const profiles=[
 {name:'Casual',runs:2,kills:500,gold:49130,rum:1881,runGold:31519,baseAdGold:512,baseAdRum:18,overflowGold:.49,overflowRum:0,upgrades:27},
 {name:'Active',runs:6,kills:1000,gold:290396,rum:9382,runGold:248751,baseAdGold:1170,baseAdRum:42,overflowGold:2.70,overflowRum:0,upgrades:34},
 {name:'Highly engaged',runs:15,kills:1600,gold:1222716,rum:34409,runGold:1154214,baseAdGold:1890,baseAdRum:54,overflowGold:2.00,overflowRum:5.84,upgrades:38}
];
const money=n=>new Intl.NumberFormat('en-US',{maximumFractionDigits:0}).format(n);

export default function RoguePiratesLiveEconomy(){
 const theme=useHostTheme();
 const [adGold,setAdGold]=useState(75),[adRum,setAdRum]=useState(5),[maxClaims,setMaxClaims]=useState(2);
 const inputStyle={width:82,padding:6,border:'1px solid '+theme.stroke.secondary,borderRadius:4,background:theme.bg.editor,color:theme.text.primary};
 const adjusted=profiles.map((p,i)=>{
  const claims=[.35,.8,1.2][i]>(maxClaims as number)?maxClaims:[.35,.8,1.2][i];
  const rumShare=[.35,.35,.30][i];
  const oldAd=p.baseAdGold+p.baseAdRum;
  const gold=p.gold-p.baseAdGold+claims*(1-rumShare)*(adGold as number)*30;
  const rum=p.rum-p.baseAdRum+claims*rumShare*(adRum as number)*30;
  return {...p,gold,rum,adG:claims*(1-rumShare)*(adGold as number)*30,adR:claims*rumShare*(adRum as number)*30,adGoldPct:100*(claims*(1-rumShare)*(adGold as number)*30)/gold,adRumPct:100*(claims*rumShare*(adRum as number)*30)/rum};
 });
 return <div style={{maxWidth:1160,margin:'auto',padding:24,color:theme.text.primary}}>
  <H1>Rogue Pirates: live economy v3.2</H1>
  <Text>180-day deterministic stress simulation · 14 September 2026 · planning assumptions, not player telemetry. Player trading is removed; all remaining run Gold banks at valid death.</Text>
  <Row><Stat label="Gold retained at death" value="100%" tone="success"/><Stat label="Functional Gold progression" value="~1.93m"/><Stat label="Shared queue duration" value="~70 days"/><Stat label="Finite Rum purchases" value="11,080"/></Row>
  <H2>Simulated monthly flows</H2>
  <Table headers={['Profile','Runs/day','Kills/run','Gold sources','Rum sources','Run + boss Gold','Gold overflow','Rum overflow','Works upgrades / 40']} rows={adjusted.map(p=>[p.name,p.runs,p.kills,money(p.gold),money(p.rum),money(p.runGold),p.overflowGold.toFixed(2)+'%',p.overflowRum.toFixed(2)+'%',p.upgrades])}/>
  <Text>Sources include runs, bosses, passive collection, LiveOps and ads. Overflow assumes the new capacities, expected raids, functional upgrades, cosmetics, targeted contracts, reforging, and voluntary Gilding. Highly engaged results assume 900k Gold/month of Gilding or comparable prestige spending; without that sink, storage caps rapidly.</Text>
  <H2>Currency sources by engagement profile</H2>
  <Text>X: player profile. Y: currency units per 30 days. Rum is shown on the same scale and is intentionally much smaller.</Text>
  <LineChart categories={adjusted.map(p=>p.name)} series={[{name:'Gold sources',data:adjusted.map(p=>p.gold),tone:'info'},{name:'Rum sources',data:adjusted.map(p=>p.rum),tone:'neutral'}]} height={260}/>
  <Text>Source: planning/game-economy-simulation.mjs, 180-day expected-value output. Categories are discrete profiles, not a time series.</Text>
  <H2>Sponsored Cache guardrail</H2>
  <div style={{display:'flex',gap:18,flexWrap:'wrap',margin:'12px 0'}}>
   <label>Gold choice <input aria-label="Gold reward" type="number" min="0" value={adGold} onChange={e=>setAdGold(Number(e.target.value))} style={inputStyle}/></label>
   <label>Rum choice <input aria-label="Rum reward" type="number" min="0" value={adRum} onChange={e=>setAdRum(Number(e.target.value))} style={inputStyle}/></label>
   <label>Daily cap <input aria-label="Daily ad claim cap" type="number" min="0" max="10" value={maxClaims} onChange={e=>setMaxClaims(Number(e.target.value))} style={inputStyle}/></label>
  </div>
  <Table headers={['Profile','Ad Gold/month','Ad Rum/month','Ads as Gold supply','Ads as Rum supply']} rows={adjusted.map(p=>[p.name,money(p.adG),money(p.adR),p.adGoldPct.toFixed(2)+'%',p.adRumPct.toFixed(2)+'%'])}/>
  <Text>Launch values are 75 Gold or 5 Rum after a completed run, at most twice daily. The model assumes average claims of 0.35, 0.80 and 1.20 daily. Keep each currency below 5% of median engaged supply; the launch settings remain below 1.1% in all three profiles.</Text>
  <H2>Where monetization sits</H2>
  <Table headers={['Surface','Placement','Economy effect','Competition rule']} rows={[
   ['Sponsored Cache','After finalized results','75 Gold or 5 Rum; two/day cap','Cannot alter completed run or score'],
   ['Portal interstitial','Between runs only','No resource reward','Never during combat or score reveal'],
   ["Captain's Chronicle",'$7.99 / 28 days','Premium track is cosmetics only','No score, power or currency'],
   ['Known cosmetic sets','$1.99–$19.99','Account-bound appearance','No combat, defense or navigation effect'],
   ['Gilding','Gold sink after capacity rank 6','Escalating vanity cost','No stat or leaderboard reward']
  ]}/>
  <H2>Current design calls</H2>
  <Text>Royal Match contributes the overlapping-event structure, month pass, social celebration and known-value offer ladder. Zen Match contributes the ten-minute habit, daily calendar, streak and decoration loops. Rogue Pirates does not use paid revival, lives/energy, direct currency sales, paid random chests, intrusive native interstitials or player resale.</Text>
  <Text>Highest uncertainty: actual kills and run length by tier, chest-buy behavior after 100% banking, demand for Gilding, contract completion rate, resource overflow, ad completion, and payer conversion. Replace assumptions after the first four-week closed economy test.</Text>
 </div>;
}
