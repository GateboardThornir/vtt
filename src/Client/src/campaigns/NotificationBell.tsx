import { useEffect, useState, type JSX } from 'react'
import { useTranslation } from 'react-i18next'
import { listNotifications, markAllRead, type NotificationView } from '../api/notifications'

/**
 * Renders notifications from a kind and a parameter.
 *
 * Task 022 deliberately sends no prose: the server has no idea which language the reader wants, so
 * the sentence is composed here in whichever one is on.
 */
export function NotificationBell(): JSX.Element | null {
  const { t } = useTranslation()
  const [items, setItems] = useState<NotificationView[]>([])
  const [open, setOpen] = useState(false)

  useEffect(() => {
    let cancelled = false

    void (async () => {
      const result = await listNotifications()

      if (!cancelled && result.kind === 'ok') {
        setItems(result.value)
      }
    })()

    return () => {
      cancelled = true
    }
  }, [])

  const unread = items.filter((item) => !item.read).length

  async function readAll(): Promise<void> {
    await markAllRead()

    const result = await listNotifications()

    if (result.kind === 'ok') {
      setItems(result.value)
    }
  }

  return (
    <div>
      <button type="button" onClick={() => setOpen(!open)}>
        {t('notifications.title')} ({t('notifications.unread', { count: unread })})
      </button>

      {open && (
        <div>
          {items.length === 0 ? (
            <p>{t('notifications.none')}</p>
          ) : (
            <>
              <ul>
                {items.map((item) => (
                  <li key={item.id}>
                    {t(`notifications.${item.kind}`, { subject: item.subject ?? '' })}
                  </li>
                ))}
              </ul>
              <button type="button" onClick={() => void readAll()}>
                {t('notifications.markAllRead')}
              </button>
            </>
          )}
        </div>
      )}
    </div>
  )
}
