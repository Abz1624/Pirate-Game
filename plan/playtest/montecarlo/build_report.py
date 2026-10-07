"""Builds report.html: the template with the simulation code and its data inlined (one self-contained page)."""
import json, os, re
here = os.path.dirname(os.path.abspath(__file__))
read = lambda n: open(os.path.join(here, n), encoding='utf-8').read()
sim = re.sub(r'^export ', '', read('sim.mjs'), flags=re.M)
html = read('report.template.html')
html = html.replace('/*SIM*/', sim).replace('/*DATA*/', read('data.json').strip()).replace('/*RESULTS*/', read('results.json').strip())
open(os.path.join(here, 'report.html'), 'w', encoding='utf-8').write(html)
print('report.html', len(html) // 1024, 'KB')
