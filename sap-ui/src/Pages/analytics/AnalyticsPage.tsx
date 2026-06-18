import { Card, CardContent, CardHeader, CardTitle } from '@/Components/ui'
import { Chart } from '@/Components/charts'
import type { EChartsOption } from 'echarts'

const barChartOption: EChartsOption = {
  tooltip: { trigger: 'axis', axisPointer: { type: 'shadow' } },
  grid: { left: '3%', right: '4%', bottom: '3%', containLabel: true },
  xAxis: {
    type: 'category',
    data: ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'],
    axisLabel: { color: '#64748b' },
  },
  yAxis: {
    type: 'value',
    axisLabel: { color: '#64748b' },
    splitLine: { lineStyle: { color: '#f1f5f9' } },
  },
  series: [
    {
      name: 'Page Views',
      type: 'bar',
      data: [320, 452, 301, 534, 490, 230, 210],
      itemStyle: { color: '#2563eb', borderRadius: [4, 4, 0, 0] },
      barWidth: '40%',
    },
    {
      name: 'Unique Visitors',
      type: 'bar',
      data: [220, 382, 201, 434, 390, 180, 160],
      itemStyle: { color: '#93c5fd', borderRadius: [4, 4, 0, 0] },
      barWidth: '40%',
    },
  ],
}

const areaChartOption: EChartsOption = {
  tooltip: { trigger: 'axis' },
  legend: { data: ['Desktop', 'Mobile', 'Tablet'], textStyle: { color: '#64748b' } },
  grid: { left: '3%', right: '4%', bottom: '3%', containLabel: true },
  xAxis: {
    type: 'category',
    boundaryGap: false,
    data: ['00:00', '04:00', '08:00', '12:00', '16:00', '20:00', '24:00'],
    axisLabel: { color: '#64748b' },
  },
  yAxis: {
    type: 'value',
    axisLabel: { color: '#64748b' },
    splitLine: { lineStyle: { color: '#f1f5f9' } },
  },
  series: [
    {
      name: 'Desktop',
      type: 'line',
      stack: 'Total',
      smooth: true,
      areaStyle: { opacity: 0.3 },
      data: [120, 132, 101, 134, 190, 230, 210],
      itemStyle: { color: '#2563eb' },
    },
    {
      name: 'Mobile',
      type: 'line',
      stack: 'Total',
      smooth: true,
      areaStyle: { opacity: 0.3 },
      data: [80, 92, 71, 94, 120, 150, 130],
      itemStyle: { color: '#60a5fa' },
    },
    {
      name: 'Tablet',
      type: 'line',
      stack: 'Total',
      smooth: true,
      areaStyle: { opacity: 0.3 },
      data: [30, 42, 31, 44, 50, 60, 55],
      itemStyle: { color: '#93c5fd' },
    },
  ],
}

export function AnalyticsPage() {
  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-slate-900">Analytics</h1>
        <p className="mt-1 text-sm text-slate-500">Track and analyze your application metrics.</p>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Weekly Traffic</CardTitle>
        </CardHeader>
        <CardContent>
          <Chart option={barChartOption} height={350} />
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Traffic by Device</CardTitle>
        </CardHeader>
        <CardContent>
          <Chart option={areaChartOption} height={350} />
        </CardContent>
      </Card>
    </div>
  )
}
