import { useEffect, useRef } from 'react'
import * as echarts from 'echarts'
import type { EChartsOption } from 'echarts'
import { cn } from '@/helpers/lib/utils'

export interface ChartProps {
  option: EChartsOption
  className?: string
  height?: number | string
  loading?: boolean
}

export function Chart({ option, className, height = 400, loading = false }: ChartProps) {
  const chartRef = useRef<HTMLDivElement>(null)
  const instanceRef = useRef<echarts.ECharts | null>(null)

  useEffect(() => {
    if (!chartRef.current) return

    instanceRef.current = echarts.init(chartRef.current)
    const instance = instanceRef.current

    const handleResize = () => instance.resize()
    window.addEventListener('resize', handleResize)

    return () => {
      window.removeEventListener('resize', handleResize)
      instance.dispose()
      instanceRef.current = null
    }
  }, [])

  useEffect(() => {
    if (!instanceRef.current) return
    instanceRef.current.setOption(option, { notMerge: true })
  }, [option])

  useEffect(() => {
    if (!instanceRef.current) return
    if (loading) {
      instanceRef.current.showLoading()
    } else {
      instanceRef.current.hideLoading()
    }
  }, [loading])

  return (
    <div
      ref={chartRef}
      className={cn('w-full', className)}
      style={{ height: typeof height === 'number' ? `${height}px` : height }}
    />
  )
}
