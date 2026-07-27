using System;

namespace s3pi.Interfaces;

public abstract class AHandlerElement : AApiVersionedFields
{
	protected EventHandler handler;

	protected bool dirty = false;

	public AHandlerElement(int APIversion, EventHandler handler)
	{
		requestedApiVersion = APIversion;
		this.handler = handler;
	}

	public abstract AHandlerElement Clone(EventHandler handler);

	protected virtual void OnElementChanged()
	{
		dirty = true;
		if (handler != null)
		{
			handler(this, EventArgs.Empty);
		}
	}
}
