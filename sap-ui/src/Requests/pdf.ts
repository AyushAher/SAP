import { apiDownload } from '@/helpers/api/client'

export async function downloadPdfTemplate(templateName: string, fileName?: string) {
  const blob = await apiDownload(`/pdf/generate`, {
    templateName,
    fileName: fileName ?? `${templateName.replace('.html', '')}.pdf`,
    placeholders: {},
  })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = fileName ?? 'document.pdf'
  a.click()
  URL.revokeObjectURL(url)
}
