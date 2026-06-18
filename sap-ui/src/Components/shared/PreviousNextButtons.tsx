import { Button } from '@/Components/ui'

interface PreviousNextButtonsProps {
  id?: string | number | null
  onPrevious?: () => void
  onNext?: () => void
  className?: string
}

export function PreviousNextButtons({ id, onPrevious, onNext, className }: PreviousNextButtonsProps) {
  const numericId = id == null || id === '' ? 0 : Number(id)
  if (!numericId || numericId <= 0) return null

  return (
    <div className={`flex gap-3 ${className ?? ''}`}>
      {numericId > 1 && onPrevious && (
        <Button type="button" variant="outline" onClick={onPrevious}>Previous</Button>
      )}
      {onNext && (
        <Button type="button" variant="outline" onClick={onNext}>Next</Button>
      )}
    </div>
  )
}
