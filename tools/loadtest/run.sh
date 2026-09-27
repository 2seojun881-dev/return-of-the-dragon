#!/bin/bash
# 단계별 부하 테스트: 로컬 게임 서버(broker.js)를 띄우고 봇 수를 늘려가며 측정
# 사용: bash run.sh "50 100 150 200 300" 10 25
STEPS=${1:-"50 100 150 200"}; K=${2:-10}; DUR=${3:-25}; PROCS=${PROCS:-3}; PORT=${PORT:-18080}
DIR=$(cd "$(dirname "$0")" && pwd); SRV=$DIR/../../server
export NODE_PATH=${NODE_PATH:-$SRV/node_modules}
for N in $STEPS; do
  MAX_PER_SERVER=100000 PORT=$PORT node $SRV/broker.js > /tmp/lt_broker.log 2>&1 & BP=$!; sleep 1.5
  per=$(( (N + PROCS - 1) / PROCS )); pids=""
  for i in $(seq 1 $PROCS); do TOTAL=$((per*PROCS)) node $DIR/bots.js $per $K $DUR ws://127.0.0.1:$PORT > /tmp/lt_$i.json & pids="$pids $!"; done
  # broker CPU% (of one core) and memory, sampled after warm-up
  sleep $((DUR/2)); t1=$(awk '{print $14+$15}' /proc/$BP/stat); sleep 5; t2=$(awk '{print $14+$15}' /proc/$BP/stat)
  cpu=$(( (${t2:-0} - ${t1:-0}) * 100 / (5 * $(getconf CLK_TCK)) )); rss=$(awk '/VmRSS/{print int($2/1024)}' /proc/$BP/status 2>/dev/null); alive=$([ -d /proc/$BP ] && echo 1 || echo 0)
  wait $pids
  python3 - "$N" "$cpu" "${rss:-0}" "$alive" <<'PY'
import json,sys,glob
rs=[json.loads(open(f).read().strip().splitlines()[-1]) for f in sorted(glob.glob('/tmp/lt_[0-9]*.json'))]
N,cpu,rss,alive=sys.argv[1:5];exp=sum(r['expectedPerSec'] for r in rs);got=sum(r['deliveredPerSec'] for r in rs)
p95=max(r['p95'] or 0 for r in rs);p99=max(r['p99'] or 0 for r in rs);p50=max(r['p50'] or 0 for r in rs);mx=max(r['max'] or 0 for r in rs)
print(json.dumps({'N':int(N),'K':rs[0]['K'],'near':rs[0].get('near'),'connected':sum(r['connected'] for r in rs),'expectedPerSec':exp,'deliveredPerSec':got,'deliveryPct':round(100*got/max(1,exp),1),'p50ms':p50,'p95ms':p95,'p99ms':p99,'maxms':mx,'brokerCpuPct':int(cpu),'brokerRssMB':int(rss),'outMbps':round(sum(r['outMbps'] for r in rs),1),'serverAlive':alive=='1'},ensure_ascii=False))
PY
  kill $BP 2>/dev/null; sleep 2
done
